using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure.Security;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using AuthRole = Api.SharedKernel.Domain.ValueObjects.Role;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for uploading private PDFs associated with UserLibraryEntry.
/// Issue #3479: Private PDF Upload Endpoint
/// </summary>
internal sealed class UploadPrivatePdfCommandHandler : ICommandHandler<UploadPrivatePdfCommand, PrivatePdfUploadResult>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IBackgroundTaskService _backgroundTaskService;
    private readonly IPdfUploadQuotaService _quotaService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UploadPrivatePdfCommandHandler> _logger;

    public UploadPrivatePdfCommandHandler(
        IUserLibraryRepository libraryRepository,
        IUserRepository userRepository,
        IPdfDocumentRepository pdfRepository,
        IBlobStorageService blobStorageService,
        IBackgroundTaskService backgroundTaskService,
        IPdfUploadQuotaService quotaService,
        IUnitOfWork unitOfWork,
        ILogger<UploadPrivatePdfCommandHandler> logger)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _pdfRepository = pdfRepository ?? throw new ArgumentNullException(nameof(pdfRepository));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _backgroundTaskService = backgroundTaskService ?? throw new ArgumentNullException(nameof(backgroundTaskService));
        _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PrivatePdfUploadResult> Handle(UploadPrivatePdfCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Validate user owns the library entry
        var libraryEntry = await _libraryRepository.GetByIdAsync(command.UserLibraryEntryId, cancellationToken).ConfigureAwait(false);

        if (libraryEntry is null)
        {
            throw new NotFoundException($"Library entry {command.UserLibraryEntryId} not found");
        }

        if (libraryEntry.UserId != command.UserId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to upload PDF to library entry {EntryId} owned by {OwnerId}",
                command.UserId, command.UserLibraryEntryId, libraryEntry.UserId);
            throw new ForbiddenException("You do not have permission to upload PDFs to this library entry");
        }

        // 2. Get user info for quota check (Issue #3653)
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            throw new NotFoundException($"User {command.UserId} not found");
        }

        var userTier = user.Tier;
        var userRole = user.Role;

        // 3. Check per-game quota (Issue #3653)
        var perGameQuotaResult = await _quotaService.CheckPerGameQuotaAsync(
            command.UserId,
            libraryEntry.GameId,
            userTier,
            userRole,
            cancellationToken).ConfigureAwait(false);

        if (!perGameQuotaResult.Allowed)
        {
            _logger.LogWarning(
                "Per-game quota exceeded for user {UserId}, game {GameId}: {Error}",
                command.UserId, libraryEntry.GameId, perGameQuotaResult.ErrorMessage);
            throw new QuotaExceededException("PerGamePdfQuota", perGameQuotaResult.ErrorMessage!);
        }

        // 4. Check daily/weekly quota
        var quotaResult = await _quotaService.CheckQuotaAsync(
            command.UserId,
            userTier,
            userRole,
            cancellationToken).ConfigureAwait(false);

        if (!quotaResult.Allowed)
        {
            _logger.LogWarning(
                "Upload quota exceeded for user {UserId}: {Error}",
                command.UserId, quotaResult.ErrorMessage);
            throw new QuotaExceededException("PdfUploadQuota", quotaResult.ErrorMessage!);
        }

        // 5. Sanitize filename
        var originalFileName = Path.GetFileName(command.PdfFile.FileName);
        string sanitizedFileName;
        try
        {
            sanitizedFileName = PathSecurity.SanitizeFilename(originalFileName);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException("FileName", $"Invalid file name: {ex.Message}");
        }

        // 3. Validate PDF structure
        using (var validationStream = command.PdfFile.OpenReadStream())
        {
            var (isValid, errorMessage) = await ValidatePdfStructureAsync(validationStream, cancellationToken).ConfigureAwait(false);
            if (!isValid)
            {
                throw new ValidationException("PdfFile", errorMessage!);
            }
        }

        // 4. Store file in blob storage
        var gameIdString = libraryEntry.GameId.ToString();
        BlobStorageResult storageResult;

        using (var stream = command.PdfFile.OpenReadStream())
        {
            storageResult = await _blobStorageService.StoreAsync(
                stream, sanitizedFileName, gameIdString, cancellationToken).ConfigureAwait(false);
        }

        if (!storageResult.Success || string.IsNullOrWhiteSpace(storageResult.FileId))
        {
            _logger.LogError(
                "Failed to store PDF file for library entry {EntryId}: {Error}",
                command.UserLibraryEntryId, storageResult.ErrorMessage);
            throw new InvalidOperationException($"Failed to store PDF file: {storageResult.ErrorMessage}");
        }

        // 5. Create PdfDocument entity (marked as private)
        var pdfId = Guid.Parse(storageResult.FileId);
        var pdfDocument = new PdfDocument(
            id: pdfId,
            gameId: libraryEntry.GameId,
            fileName: new FileName(sanitizedFileName),
            filePath: storageResult.FilePath!,
            fileSize: new FileSize(storageResult.FileSizeBytes),
            uploadedByUserId: command.UserId,
            language: LanguageCode.English // Default, will be detected during processing
        );

        // Mark as private
        pdfDocument.MakePrivate();

        await _pdfRepository.AddAsync(pdfDocument, cancellationToken).ConfigureAwait(false);

        // 6. Associate PDF with library entry (triggers PrivatePdfAssociatedEvent)
        libraryEntry.AssociatePrivatePdf(pdfId);

        // 11. Save all changes
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 12. Increment quota counts (Issue #3653)
        await _quotaService.IncrementUploadCountAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        await _quotaService.IncrementPerGameCountAsync(command.UserId, libraryEntry.GameId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Private PDF {PdfId} uploaded for library entry {EntryId} (Game: {GameId}, User: {UserId})",
            pdfId, command.UserLibraryEntryId, libraryEntry.GameId, command.UserId);

        // 13. Start background processing (extract, chunk, embed, index)
        _backgroundTaskService.ExecuteWithCancellation(
            storageResult.FileId,
            (ct) => TriggerPdfProcessingAsync(pdfId, storageResult.FilePath!, command.UserId, ct));

        // 14. Return result with SSE stream URL and quota info (Issue #3653)
        var sseStreamUrl = $"/api/v1/users/{command.UserId}/library/{command.UserLibraryEntryId}/pdf/progress";

        // Calculate remaining quotas after this upload
        var quotaRemaining = new QuotaRemainingInfo(
            Daily: Math.Max(0, quotaResult.DailyLimit - quotaResult.DailyUploadsUsed - 1),
            Weekly: Math.Max(0, quotaResult.WeeklyLimit - quotaResult.WeeklyUploadsUsed - 1),
            PerGame: Math.Max(0, perGameQuotaResult.PerGameLimit - perGameQuotaResult.PerGameUsed - 1)
        );

        return new PrivatePdfUploadResult(
            PdfId: pdfId,
            FileName: sanitizedFileName,
            FileSize: storageResult.FileSizeBytes,
            Status: "processing",
            SseStreamUrl: sseStreamUrl,
            QuotaRemaining: quotaRemaining
        );
    }

    /// <summary>
    /// Validates PDF file structure by checking for required PDF headers and trailers.
    /// </summary>
    private static async Task<(bool IsValid, string? ErrorMessage)> ValidatePdfStructureAsync(
        Stream stream, CancellationToken cancellationToken)
    {
        const int headerCheckBytes = 1024;
        const int trailerCheckBytes = 1024;

        try
        {
            if (stream.Length < 50)
            {
                return (false, "Invalid PDF file: File is too small to be a valid PDF.");
            }

            // Check PDF header
            stream.Seek(0, SeekOrigin.Begin);
            var headerBuffer = new byte[Math.Min(headerCheckBytes, (int)stream.Length)];
            var headerBytesRead = await stream.ReadAsync(headerBuffer.AsMemory(0, headerBuffer.Length), cancellationToken).ConfigureAwait(false);

            var headerText = System.Text.Encoding.ASCII.GetString(headerBuffer, 0, Math.Min(10, headerBytesRead));
            if (!headerText.StartsWith("%PDF-", StringComparison.Ordinal))
            {
                return (false, "Invalid PDF file: Missing PDF header signature.");
            }

            // Check PDF trailer
            var trailerStart = Math.Max(0, stream.Length - trailerCheckBytes);
            stream.Seek(trailerStart, SeekOrigin.Begin);
            var trailerBuffer = new byte[Math.Min(trailerCheckBytes, (int)(stream.Length - trailerStart))];
            var trailerBytesRead = await stream.ReadAsync(trailerBuffer.AsMemory(0, trailerBuffer.Length), cancellationToken).ConfigureAwait(false);

            var trailerText = System.Text.Encoding.ASCII.GetString(trailerBuffer, 0, trailerBytesRead);
            if (!trailerText.Contains("%%EOF", StringComparison.Ordinal))
            {
                return (false, "Invalid PDF file: Missing PDF end-of-file marker.");
            }

            stream.Seek(0, SeekOrigin.Begin);
            return (true, null);
        }
        catch (Exception ex)
        {
            try { stream.Seek(0, SeekOrigin.Begin); } catch { /* Ignore */ }
            return (false, $"Failed to validate PDF structure: {ex.Message}");
        }
    }

    /// <summary>
    /// Placeholder for triggering PDF processing pipeline.
    /// The actual processing will be handled by the existing PDF processing infrastructure.
    /// </summary>
    private Task TriggerPdfProcessingAsync(Guid pdfId, string filePath, Guid userId, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Triggering PDF processing for private PDF {PdfId} (User: {UserId})",
            pdfId, userId);

        // The actual processing is handled by the existing infrastructure
        // This method just logs the trigger - processing is done via event handlers
        // listening to PrivatePdfAssociatedEvent
        return Task.CompletedTask;
    }
}

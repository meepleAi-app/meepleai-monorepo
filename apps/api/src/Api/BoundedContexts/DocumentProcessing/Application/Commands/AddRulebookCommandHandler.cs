using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.EntityRelationships.Application.Commands;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Exceptions;
using Api.Constants;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Security;
using Api.Models;
using Api.Services;
using Api.Services.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Exceptions;
using Api.SharedKernel.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handles rulebook upload with PDF deduplication.
/// When a matching content hash exists and the document is non-Failed,
/// reuses the existing document by creating an EntityLink.
/// When no match exists or the match is Failed, performs a full upload.
/// </summary>
internal sealed class AddRulebookCommandHandler : ICommandHandler<AddRulebookCommand, RulebookUploadResult>
{
    private readonly IPdfDocumentRepository _pdfDocumentRepository;
    private readonly MeepleAiDbContext _db;
    private readonly IMediator _mediator;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ITierEnforcementService _tierEnforcementService;
    private readonly IBackgroundTaskService _backgroundTaskService;
    private readonly IPdfUploadQuotaService _quotaService;
    private readonly ILogger<AddRulebookCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public AddRulebookCommandHandler(
        IPdfDocumentRepository pdfDocumentRepository,
        MeepleAiDbContext db,
        IMediator mediator,
        IBlobStorageService blobStorageService,
        ITierEnforcementService tierEnforcementService,
        IBackgroundTaskService backgroundTaskService,
        IPdfUploadQuotaService quotaService,
        ILogger<AddRulebookCommandHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _pdfDocumentRepository = pdfDocumentRepository ?? throw new ArgumentNullException(nameof(pdfDocumentRepository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _tierEnforcementService = tierEnforcementService ?? throw new ArgumentNullException(nameof(tierEnforcementService));
        _backgroundTaskService = backgroundTaskService ?? throw new ArgumentNullException(nameof(backgroundTaskService));
        _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RulebookUploadResult> Handle(AddRulebookCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var file = command.File;
        var userId = command.UserId;
        var gameId = command.GameId;

        // Step 0: Validate game exists and user owns it
        var game = await _db.Games
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken)
            .ConfigureAwait(false);

        if (game is null)
        {
            throw new Middleware.Exceptions.NotFoundException($"Game {gameId} not found.");
        }

        var ownsGame = await _db.UserLibraryEntries
            .AnyAsync(ule => ule.UserId == userId && ule.SharedGameId == gameId, cancellationToken)
            .ConfigureAwait(false);

        if (!ownsGame)
        {
            throw new Middleware.Exceptions.ForbiddenException($"User {userId} does not own game {gameId}.");
        }

        // Step 1: Compute SHA-256 hash
        var contentHash = await ComputeContentHashAsync(file, cancellationToken).ConfigureAwait(false);

        // Step 2: Look up existing document by hash
        var existingDoc = await _pdfDocumentRepository
            .FindByContentHashAsync(contentHash, cancellationToken)
            .ConfigureAwait(false);

        if (existingDoc is not null)
        {
            return await HandleExistingDocumentAsync(existingDoc, gameId, userId, file, contentHash, cancellationToken)
                .ConfigureAwait(false);
        }

        // Step 3: No match — full upload flow
        return await HandleNewUploadAsync(gameId, userId, file, contentHash, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Handles the case where a document with matching content hash already exists.
    /// For Ready or in-progress states, reuses by creating an EntityLink.
    /// For Failed state, treats as a new upload.
    /// </summary>
    private async Task<RulebookUploadResult> HandleExistingDocumentAsync(
        Domain.Entities.PdfDocument existingDoc,
        Guid gameId,
        Guid userId,
        IFormFile file,
        string contentHash,
        CancellationToken cancellationToken)
    {
        var state = existingDoc.ProcessingState;

        if (state == PdfProcessingState.Failed)
        {
            _logger.LogInformation(
                "Found existing PDF {PdfId} with matching hash but Failed state — treating as new upload",
                existingDoc.Id);

            // Clean up stale EntityLinks from this game to the failed PDF
            await CleanupStaleEntityLinksAsync(existingDoc.Id, gameId, cancellationToken).ConfigureAwait(false);

            return await HandleNewUploadAsync(gameId, userId, file, contentHash, cancellationToken)
                .ConfigureAwait(false);
        }

        // Ready or in-progress: reuse by creating EntityLink
        await CreateKbCardEntityLinkSafelyAsync(existingDoc.Id, gameId, userId, cancellationToken)
            .ConfigureAwait(false);

        var status = RulebookUploadResult.MapStatus(state);
        var message = state == PdfProcessingState.Ready
            ? "Regolamento già disponibile — collegato al tuo gioco!"
            : "Regolamento in elaborazione — sarà disponibile a breve.";

        _logger.LogInformation(
            "Reused existing PDF {PdfId} (state={State}) for game {GameId} by user {UserId}",
            existingDoc.Id, state, gameId, userId);

        return new RulebookUploadResult(
            PdfDocumentId: existingDoc.Id,
            IsNew: false,
            Status: status,
            Message: message);
    }

    /// <summary>
    /// Performs a full new upload: validates, stores, creates DB record, starts processing.
    /// </summary>
    private async Task<RulebookUploadResult> HandleNewUploadAsync(
        Guid gameId,
        Guid userId,
        IFormFile file,
        string contentHash,
        CancellationToken cancellationToken)
    {
        // Tier enforcement: check upload quota
        var canUpload = await _tierEnforcementService
            .CanPerformAsync(userId, TierAction.UploadPdf, cancellationToken)
            .ConfigureAwait(false);

        if (!canUpload)
        {
            var usage = await _tierEnforcementService
                .GetUsageAsync(userId, cancellationToken)
                .ConfigureAwait(false);

            throw new TierLimitExceededException(
                nameof(TierAction.UploadPdf),
                usage.PdfThisMonth,
                usage.PdfThisMonthMax);
        }

        // Tier enforcement: check file size
        var tierLimits = await _tierEnforcementService
            .GetLimitsAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        if (file.Length > tierLimits.MaxPdfSizeBytes)
        {
            var maxMb = tierLimits.MaxPdfSizeBytes / (1024 * 1024);
            throw new TierLimitExceededException(
                "PdfFileSize",
                $"File size ({file.Length / (1024 * 1024)} MB) exceeds your tier limit ({maxMb} MB). Upgrade to upload larger files.");
        }

        // Validate PDF structure (check %PDF header)
        await ValidatePdfStructureAsync(file, cancellationToken).ConfigureAwait(false);

        // Sanitize filename
        var fileName = SanitizeFileName(file.FileName);

        // Store file in blob storage
        BlobStorageResult storageResult;
        using (var stream = file.OpenReadStream())
        {
            storageResult = await _blobStorageService
                .StoreAsync(stream, fileName, gameId.ToString(), cancellationToken)
                .ConfigureAwait(false);
        }

        if (!storageResult.Success || string.IsNullOrWhiteSpace(storageResult.FileId))
        {
            throw new PdfStorageException("Failed to store PDF file in blob storage.");
        }

        // Create database record
        var pdfDoc = new PdfDocumentEntity
        {
            Id = Guid.Parse(storageResult.FileId!),
            GameId = gameId,
            FileName = fileName,
            FilePath = storageResult.FilePath!,
            FileSizeBytes = storageResult.FileSizeBytes,
            ContentType = file.ContentType,
            UploadedByUserId = userId,
            UploadedAt = _timeProvider.GetUtcNow().UtcDateTime,
            ContentHash = contentHash,
            DocumentCategory = "Rulebook"
        };

        _db.PdfDocuments.Add(pdfDoc);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created PDF document record {PdfId} for game {GameId} via rulebook upload",
            pdfDoc.Id, gameId);

        // Initialize progress tracking
        pdfDoc.ProcessingProgress = new ProcessingProgress
        {
            CurrentStep = ProcessingStep.Uploading,
            PercentComplete = 20,
            ElapsedTime = TimeSpan.Zero,
            EstimatedTimeRemaining = null,
            PagesProcessed = 0,
            TotalPages = 0,
            StartedAt = _timeProvider.GetUtcNow().UtcDateTime,
            CompletedAt = null,
            ErrorMessage = null
        };
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Reserve quota
        var reservationResult = await _quotaService
            .ReserveQuotaAsync(userId, storageResult.FileId!, cancellationToken)
            .ConfigureAwait(false);

        if (!reservationResult.Reserved)
        {
            // Clean up on quota failure
            try
            {
                await _blobStorageService.DeleteAsync(storageResult.FileId!, gameId.ToString(), cancellationToken).ConfigureAwait(false);
                _db.PdfDocuments.Remove(pdfDoc);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Failed to cleanup after quota reservation failure for PDF {PdfId}", storageResult.FileId);
            }
#pragma warning restore CA1031

            throw new TierLimitExceededException("UploadPdf", reservationResult.ErrorMessage ?? "Quota reservation failed.");
        }

        // Start background processing (fire-and-forget)
        _backgroundTaskService.ExecuteWithCancellation(
            storageResult.FileId!,
            (ct) => ProcessPdfInBackgroundAsync(storageResult.FileId!, storageResult.FilePath!, userId, ct));

        // Enqueue as Quartz fallback
        await EnqueueForProcessingSafelyAsync(pdfDoc.Id, userId, cancellationToken).ConfigureAwait(false);

        // Create EntityLink
        await CreateKbCardEntityLinkSafelyAsync(pdfDoc.Id, gameId, userId, cancellationToken).ConfigureAwait(false);

        return new RulebookUploadResult(
            PdfDocumentId: pdfDoc.Id,
            IsNew: true,
            Status: "pending",
            Message: "Regolamento caricato con successo.");
    }

    /// <summary>
    /// Creates an EntityLink Game→KbCard safely (idempotent, non-critical).
    /// Mirrors the pattern from UploadPdfCommandHandler.
    /// </summary>
    private async Task CreateKbCardEntityLinkSafelyAsync(
        Guid pdfDocumentId,
        Guid gameId,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        if (gameId == Guid.Empty)
            return;

        try
        {
            var cmd = new CreateEntityLinkCommand(
                SourceEntityType: MeepleEntityType.Game,
                SourceEntityId: gameId,
                TargetEntityType: MeepleEntityType.KbCard,
                TargetEntityId: pdfDocumentId,
                LinkType: EntityLinkType.RelatedTo,
                Scope: EntityLinkScope.User,
                OwnerUserId: ownerUserId
            );

            await _mediator.Send(cmd, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug(
                "EntityLink Game/{GameId} → KbCard/{PdfId} created for user {UserId}",
                gameId, pdfDocumentId, ownerUserId);
        }
        catch (DuplicateEntityLinkException ex)
        {
            // Idempotent: link already exists. This is expected.
            _logger.LogDebug(
                ex,
                "EntityLink Game/{GameId} → KbCard/{PdfId} already exists — skipping",
                gameId, pdfDocumentId);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            // Non-critical: log but do not fail the upload
            _logger.LogWarning(
                ex,
                "Failed to create EntityLink for PDF {PdfId} → Game {GameId}. Upload still succeeded.",
                pdfDocumentId, gameId);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Cleans up stale EntityLinks for a failed PDF document.
    /// </summary>
    private async Task CleanupStaleEntityLinksAsync(Guid pdfDocumentId, Guid gameId, CancellationToken cancellationToken)
    {
        try
        {
            var staleLinks = await _db.EntityLinks
                .Where(el => el.TargetEntityId == pdfDocumentId
                    && el.TargetEntityType == MeepleEntityType.KbCard
                    && el.SourceEntityId == gameId
                    && el.SourceEntityType == MeepleEntityType.Game)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (staleLinks.Count > 0)
            {
                _db.EntityLinks.RemoveRange(staleLinks);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "Cleaned up {Count} stale EntityLinks for failed PDF {PdfId}",
                    staleLinks.Count, pdfDocumentId);
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup stale EntityLinks for PDF {PdfId}", pdfDocumentId);
        }
#pragma warning restore CA1031
    }

    private static async Task<string> ComputeContentHashAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var stream = file.OpenReadStream();
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hashBytes);
    }

    private static async Task ValidatePdfStructureAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var stream = file.OpenReadStream();

        if (stream.Length < 50)
            throw new PdfStorageException("Invalid PDF file: File is too small to be a valid PDF.");

        stream.Seek(0, SeekOrigin.Begin);
        var headerBuffer = new byte[Math.Min(1024, (int)stream.Length)];
        var headerBytesRead = await stream.ReadAsync(headerBuffer.AsMemory(0, headerBuffer.Length), cancellationToken).ConfigureAwait(false);

        var headerText = System.Text.Encoding.ASCII.GetString(headerBuffer, 0, Math.Min(10, headerBytesRead));
        if (!headerText.StartsWith("%PDF-", StringComparison.Ordinal))
            throw new PdfStorageException("Invalid PDF file: Missing PDF header signature.");
    }

    private static string SanitizeFileName(string originalFileName)
    {
        var fileName = Path.GetFileName(originalFileName);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new PdfStorageException("Invalid file name.");

        return PathSecurity.SanitizeFilename(fileName);
    }

    /// <summary>
    /// Delegates background processing to the existing pipeline via scope factory.
    /// The actual heavy processing (extract, chunk, embed, index) is handled by the
    /// Quartz job that picks up the enqueued PDF. This fire-and-forget is a no-op
    /// placeholder matching UploadPdfCommandHandler's pattern.
    /// </summary>
    private Task ProcessPdfInBackgroundAsync(string pdfId, string filePath, Guid userId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Rulebook PDF {PdfId} background processing delegated to Quartz pipeline", pdfId);
        return Task.CompletedTask;
    }

    private async Task EnqueueForProcessingSafelyAsync(Guid pdfDocumentId, Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(
                new Queue.EnqueuePdfCommand(pdfDocumentId, userId, Priority: 0),
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Rulebook PDF {PdfId} enqueued for Quartz processing", pdfDocumentId);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not enqueue PDF {PdfId} for Quartz processing (may already be queued)", pdfDocumentId);
        }
#pragma warning restore CA1031
    }
}

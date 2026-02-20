using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Security;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for UploadSharedGamePdfCommand.
/// Uploads a PDF for a shared game as admin, bypassing user library and quotas.
/// Issue #4922: Admin upload endpoint for shared game documents.
/// </summary>
internal sealed class UploadSharedGamePdfCommandHandler
    : ICommandHandler<UploadSharedGamePdfCommand, UploadSharedGamePdfResult>
{
    private readonly ISharedGameRepository _gameRepository;
    private readonly MeepleAiDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IPdfProcessingPipelineService _processingPipeline;
    private readonly IBackgroundTaskService _backgroundTaskService;
    private readonly IMediator _mediator;
    private readonly ILogger<UploadSharedGamePdfCommandHandler> _logger;

    private const long MaxFileSizeBytes = 500L * 1024 * 1024; // 500 MB
    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.Ordinal) { "application/pdf" };

    public UploadSharedGamePdfCommandHandler(
        ISharedGameRepository gameRepository,
        MeepleAiDbContext db,
        IUnitOfWork unitOfWork,
        IBlobStorageService blobStorageService,
        IPdfProcessingPipelineService processingPipeline,
        IBackgroundTaskService backgroundTaskService,
        IMediator mediator,
        ILogger<UploadSharedGamePdfCommandHandler> logger)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _processingPipeline = processingPipeline ?? throw new ArgumentNullException(nameof(processingPipeline));
        _backgroundTaskService = backgroundTaskService ?? throw new ArgumentNullException(nameof(backgroundTaskService));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UploadSharedGamePdfResult> Handle(
        UploadSharedGamePdfCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Validate SharedGame exists
        _ = await _gameRepository.GetByIdAsync(command.SharedGameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("SharedGame", command.SharedGameId.ToString());

        _logger.LogInformation(
            "Admin {AdminUserId} uploading PDF for shared game {SharedGameId}, type: {DocumentType}, version: {Version}",
            command.AdminUserId, command.SharedGameId, command.DocumentType, command.Version);

        // 2. Validate file
        ValidateFile(command.File);

        // 3. Sanitize filename
        var fileName = Path.GetFileName(command.File.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Invalid file name.", nameof(command));
        fileName = PathSecurity.SanitizeFilename(fileName);

        // 4. Store file in blob storage
        using var fileStream = command.File.OpenReadStream();
        var storageResult = await _blobStorageService.StoreAsync(
            fileStream,
            fileName,
            gameId: $"shared-game-{command.SharedGameId}",
            ct: cancellationToken).ConfigureAwait(false);

        if (!storageResult.Success)
        {
            _logger.LogError(
                "Failed to store PDF for shared game {SharedGameId}: {Error}",
                command.SharedGameId, storageResult.ErrorMessage);
            throw new InvalidOperationException(storageResult.ErrorMessage ?? "Failed to store file");
        }

        // 5. Create PdfDocumentEntity with SharedGameId (no GameId = user library link)
        var pdfDocumentId = Guid.NewGuid();
        var pdfEntity = new PdfDocumentEntity
        {
            Id = pdfDocumentId,
            SharedGameId = command.SharedGameId,
            GameId = Guid.Empty, // Required column — use sentinel; real game is via SharedGameId
            FileName = fileName,
            FilePath = storageResult.FilePath!,
            FileSizeBytes = storageResult.FileSizeBytes,
            ContentType = command.File.ContentType,
            UploadedByUserId = command.AdminUserId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Pending",
            ProcessingStatus = "pending",
            ProcessingPriority = "Admin",
            IsPublic = true,
            DocumentType = command.DocumentType.ToString().ToLowerInvariant()
        };

        await _db.PdfDocuments.AddAsync(pdfEntity, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 6. Create SharedGameDocument via existing command
        var sharedGameDocId = await _mediator.Send(
            new AddDocumentToSharedGameCommand(
                command.SharedGameId,
                pdfDocumentId,
                command.DocumentType,
                command.Version,
                command.Tags,
                command.SetAsActive,
                command.AdminUserId),
            cancellationToken).ConfigureAwait(false);

        // 7. Enqueue PDF processing in background
        var pdfDocId = pdfDocumentId;
        var filePath = storageResult.FilePath!;
        var adminId = command.AdminUserId;
        _backgroundTaskService.Execute(async () =>
        {
            await _processingPipeline.ProcessAsync(pdfDocId, filePath, adminId, CancellationToken.None)
                .ConfigureAwait(false);
        });

        _logger.LogInformation(
            "Shared game PDF upload complete: PdfDocumentId={PdfDocumentId}, SharedGameDocumentId={SharedGameDocumentId}",
            pdfDocumentId, sharedGameDocId);

        return new UploadSharedGamePdfResult(pdfDocumentId, sharedGameDocId, "processing");
    }

    private static void ValidateFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("No file provided. Please select a PDF file to upload.", nameof(file));

        if (file.Length > MaxFileSizeBytes)
        {
            var sizeMB = file.Length / 1024.0 / 1024.0;
            throw new ArgumentException(
                $"File is too large ({sizeMB:F1}MB). Maximum size for admin upload is 500MB.", nameof(file));
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
            throw new ArgumentException($"Invalid file type ({file.ContentType}). Only PDF files are allowed.", nameof(file));
    }
}

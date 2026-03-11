using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;

/// <summary>
/// Saga handler that orchestrates the RAG wizard workflow:
/// 1. Validate SharedGame exists
/// 2. Upload PDF via UploadPdfCommand
/// 3. Link document via AddDocumentToSharedGameCommand
/// 4. (Admin only) Auto-approve + enqueue for processing
/// </summary>
internal sealed class AddRagToSharedGameCommandHandler : ICommandHandler<AddRagToSharedGameCommand, AddRagToSharedGameResult>
{
    private readonly IMediator _mediator;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly ILogger<AddRagToSharedGameCommandHandler> _logger;

    public AddRagToSharedGameCommandHandler(
        IMediator mediator,
        ISharedGameRepository sharedGameRepository,
        ILogger<AddRagToSharedGameCommandHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AddRagToSharedGameResult> Handle(AddRagToSharedGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Starting RAG wizard saga for SharedGame {SharedGameId}, User {UserId}, IsAdmin={IsAdmin}",
            command.SharedGameId, command.UserId, command.IsAdmin);

        // Step 1: Validate SharedGame exists
        var sharedGame = await _sharedGameRepository.GetByIdAsync(command.SharedGameId, cancellationToken).ConfigureAwait(false);
        if (sharedGame is null)
        {
            throw new NotFoundException("SharedGame", command.SharedGameId.ToString());
        }

        // Step 2: Upload PDF
        var uploadResult = await _mediator.Send(
            new UploadPdfCommand(
                GameId: null,
                Metadata: null,
                PrivateGameId: null,
                UserId: command.UserId,
                File: command.File),
            cancellationToken).ConfigureAwait(false);

        if (!uploadResult.Success || uploadResult.Document is null)
        {
            throw new ConflictException($"PDF upload failed: {uploadResult.Message}");
        }

        var pdfDocumentId = uploadResult.Document.Id;

        _logger.LogInformation(
            "PDF uploaded successfully: PdfDocumentId={PdfDocumentId}",
            pdfDocumentId);

        // Step 3: Link document to SharedGame
        var sharedGameDocumentId = await _mediator.Send(
            new AddDocumentToSharedGameCommand(
                SharedGameId: command.SharedGameId,
                PdfDocumentId: pdfDocumentId,
                DocumentType: command.DocumentType,
                Version: command.Version,
                Tags: command.Tags,
                SetAsActive: true,
                CreatedBy: command.UserId),
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Document linked to SharedGame: SharedGameDocumentId={SharedGameDocumentId}",
            sharedGameDocumentId);

        // Step 4: Admin auto-approval and enqueue
        if (!command.IsAdmin)
        {
            _logger.LogInformation(
                "Editor upload complete — pending approval. SharedGameDocumentId={SharedGameDocumentId}",
                sharedGameDocumentId);

            return new AddRagToSharedGameResult(
                PdfDocumentId: pdfDocumentId,
                SharedGameDocumentId: sharedGameDocumentId,
                ProcessingJobId: null,
                AutoApproved: false,
                StreamUrl: $"/api/v1/pdf/{pdfDocumentId}/status/stream");
        }

        // Admin path: auto-approve
        await _mediator.Send(
            new ApproveDocumentForRagProcessingCommand(
                DocumentId: sharedGameDocumentId,
                ApprovedBy: command.UserId,
                Notes: "Auto-approved via RAG wizard (admin)"),
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Document auto-approved for RAG processing: SharedGameDocumentId={SharedGameDocumentId}",
            sharedGameDocumentId);

        // Admin path: enqueue (graceful degradation on failure)
        Guid? processingJobId = null;
        try
        {
            processingJobId = await _mediator.Send(
                new EnqueuePdfCommand(
                    PdfDocumentId: pdfDocumentId,
                    UserId: command.UserId,
                    Priority: (int)ProcessingPriority.High),
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "PDF enqueued for processing: ProcessingJobId={ProcessingJobId}",
                processingJobId);
        }
        catch (OperationCanceledException)
        {
            throw; // propagate cancellation
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to enqueue PDF for processing (graceful degradation): PdfDocumentId={PdfDocumentId}",
                pdfDocumentId);
        }

        return new AddRagToSharedGameResult(
            PdfDocumentId: pdfDocumentId,
            SharedGameDocumentId: sharedGameDocumentId,
            ProcessingJobId: processingJobId,
            AutoApproved: true,
            StreamUrl: $"/api/v1/pdf/{pdfDocumentId}/status/stream");
    }
}

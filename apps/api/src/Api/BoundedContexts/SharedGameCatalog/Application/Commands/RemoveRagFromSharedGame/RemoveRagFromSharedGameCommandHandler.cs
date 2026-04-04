using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.RemoveRagFromSharedGame;

/// <summary>
/// Saga handler that orchestrates full PDF removal from a SharedGame:
/// 1. Validate SharedGameDocument exists and belongs to game
/// 2. Resolve PdfDocumentId
/// 3. Auto-promote next version if removing active document
/// 4. Remove SharedGameDocument link (via RemoveDocumentFromSharedGameCommand)
/// 5. Delete PDF with full cleanup (via DeletePdfCommand — cascades VectorDoc, TextChunks, Qdrant, blob)
/// </summary>
internal sealed class RemoveRagFromSharedGameCommandHandler
    : ICommandHandler<RemoveRagFromSharedGameCommand, RemoveRagFromSharedGameResult>
{
    private readonly IMediator _mediator;
    private readonly ISharedGameDocumentRepository _documentRepository;
    private readonly ILogger<RemoveRagFromSharedGameCommandHandler> _logger;

    public RemoveRagFromSharedGameCommandHandler(
        IMediator mediator,
        ISharedGameDocumentRepository documentRepository,
        ILogger<RemoveRagFromSharedGameCommandHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RemoveRagFromSharedGameResult> Handle(
        RemoveRagFromSharedGameCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Starting RAG removal saga for SharedGame {SharedGameId}, Document {DocumentId}, User {UserId}",
            command.SharedGameId, command.SharedGameDocumentId, command.UserId);

        // Step 1: Validate document exists and belongs to game
        var document = await _documentRepository.GetByIdAsync(
            command.SharedGameDocumentId, cancellationToken).ConfigureAwait(false);

        if (document is null || document.SharedGameId != command.SharedGameId)
        {
            throw new NotFoundException("SharedGameDocument", command.SharedGameDocumentId.ToString());
        }

        var pdfDocumentId = document.PdfDocumentId;

        // Step 2: Auto-promote next version if removing active document
        if (document.IsActive)
        {
            var sameTypeVersions = await _documentRepository.GetBySharedGameIdAndTypeAsync(
                command.SharedGameId, document.DocumentType, cancellationToken).ConfigureAwait(false);

            var nextVersion = sameTypeVersions
                .Where(d => d.Id != command.SharedGameDocumentId)
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefault();

            if (nextVersion is not null)
            {
                nextVersion.SetAsActive();
                _documentRepository.Update(nextVersion);

                _logger.LogInformation(
                    "Auto-promoted document {NextDocId} (v{Version}) to active for {DocType}",
                    nextVersion.Id, nextVersion.Version, document.DocumentType);
            }
        }

        // Step 3: Remove SharedGameDocument link
        await _mediator.Send(
            new RemoveDocumentFromSharedGameCommand(command.SharedGameId, command.SharedGameDocumentId),
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "SharedGameDocument {DocumentId} unlinked from game {SharedGameId}",
            command.SharedGameDocumentId, command.SharedGameId);

        // Step 4: Delete PDF with full cleanup (best-effort for external systems)
        bool qdrantOk = true;
        bool blobOk = true;

        try
        {
            var deleteResult = await _mediator.Send(
                new DeletePdfCommand(pdfDocumentId.ToString()),
                cancellationToken).ConfigureAwait(false);

            if (!deleteResult.Success)
            {
                _logger.LogWarning(
                    "PDF deletion reported failure for {PdfId}: {Message}",
                    pdfDocumentId, deleteResult.Message);
                qdrantOk = false;
                blobOk = false;
            }
        }
        catch (OperationCanceledException)
        {
            throw; // propagate cancellation
        }
#pragma warning disable CA1031 // SERVICE BOUNDARY: Multi-system cleanup with graceful degradation
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to delete PDF {PdfId} (graceful degradation — SharedGameDocument already removed)",
                pdfDocumentId);
            qdrantOk = false;
            blobOk = false;
        }
#pragma warning restore CA1031

        _logger.LogInformation(
            "RAG removal saga completed: SharedGameDocument={DocId}, PDF={PdfId}, Qdrant={Qdrant}, Blob={Blob}",
            command.SharedGameDocumentId, pdfDocumentId, qdrantOk, blobOk);

        return new RemoveRagFromSharedGameResult(
            RemovedSharedGameDocumentId: command.SharedGameDocumentId,
            RemovedPdfDocumentId: pdfDocumentId,
            QdrantCleanupSucceeded: qdrantOk,
            BlobCleanupSucceeded: blobOk);
    }
}

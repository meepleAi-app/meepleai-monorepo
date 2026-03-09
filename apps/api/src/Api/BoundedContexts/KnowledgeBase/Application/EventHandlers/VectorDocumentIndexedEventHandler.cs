using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Api.SharedKernel.Application.IntegrationEvents;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Handles VectorDocumentIndexedEvent by publishing a cross-context integration event.
/// Issue #4942: Original notification handler.
/// Issue #5237: Refactored to remove cross-context coupling — notification and state
/// updates are now handled by UserNotifications and DocumentProcessing respectively
/// via VectorDocumentReadyIntegrationEvent.
/// </summary>
internal sealed class VectorDocumentIndexedEventHandler : DomainEventHandlerBase<VectorDocumentIndexedEvent>
{
    private readonly IVectorDocumentRepository _vectorDocRepo;
    private readonly IPdfDocumentRepository _pdfDocRepo;
    private readonly IMediator _mediator;

    public VectorDocumentIndexedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<VectorDocumentIndexedEventHandler> logger,
        IVectorDocumentRepository vectorDocRepo,
        IPdfDocumentRepository pdfDocRepo,
        IMediator mediator)
        : base(dbContext, logger)
    {
        _vectorDocRepo = vectorDocRepo;
        _pdfDocRepo = pdfDocRepo;
        _mediator = mediator;
    }

    protected override async Task HandleEventAsync(
        VectorDocumentIndexedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        // Retrieve the VectorDocument to get the PdfDocumentId
        var vectorDoc = await _vectorDocRepo.GetByIdAsync(domainEvent.DocumentId, cancellationToken).ConfigureAwait(false);
        if (vectorDoc == null)
        {
            Logger.LogWarning(
                "VectorDocumentIndexedEventHandler: VectorDocument {DocumentId} not found, skipping",
                domainEvent.DocumentId);
            return;
        }

        // Retrieve the PDF document to get UploadedByUserId and file name
        var pdfDoc = await _pdfDocRepo.GetByIdAsync(vectorDoc.PdfDocumentId, cancellationToken).ConfigureAwait(false);
        if (pdfDoc == null)
        {
            Logger.LogWarning(
                "VectorDocumentIndexedEventHandler: PdfDocument {PdfId} not found for VectorDocument {DocumentId}, skipping",
                vectorDoc.PdfDocumentId,
                domainEvent.DocumentId);
            return;
        }

        // Publish integration event for cross-context consumers:
        // - DocumentProcessing: compensating state update (ProcessingState → Ready)
        // - UserNotifications: in-app, email, push notifications
        await _mediator.Publish(new VectorDocumentReadyIntegrationEvent
        {
            DocumentId = domainEvent.DocumentId,
            GameId = domainEvent.GameId,
            ChunkCount = domainEvent.ChunkCount,
            PdfDocumentId = pdfDoc.Id,
            UploadedByUserId = pdfDoc.UploadedByUserId,
            FileName = pdfDoc.FileName.Value,
            CurrentProcessingState = pdfDoc.ProcessingState.ToString()
        }, cancellationToken).ConfigureAwait(false);

        Logger.LogInformation(
            "Published VectorDocumentReadyIntegrationEvent for document {DocumentId}, game {GameId}, user {UserId}",
            domainEvent.DocumentId,
            domainEvent.GameId,
            pdfDoc.UploadedByUserId);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(VectorDocumentIndexedEvent domainEvent)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Action"] = "VectorDocumentIndexed",
            ["DocumentId"] = domainEvent.DocumentId,
            ["GameId"] = domainEvent.GameId,
            ["ChunkCount"] = domainEvent.ChunkCount
        };
    }
}

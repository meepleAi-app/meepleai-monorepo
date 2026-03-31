using Api.BoundedContexts.UserLibrary.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Handles PrivatePdfAssociatedEvent fired when a user associates a private PDF with a game.
/// NOTE: At this point the PDF has NOT yet been indexed — no VectorDocument exists yet.
/// Auto-adding the document to the agent's SelectedDocumentIds must happen AFTER indexing
/// is complete, which is handled by <see cref="AgentDocumentSyncOnReadyHandler"/> listening
/// to VectorDocumentReadyIntegrationEvent.
/// This handler is intentionally a no-op to avoid writing PdfDocument.Id into
/// SelectedDocumentIdsJson, which expects VectorDocument.Id values.
/// </summary>
internal sealed class PrivatePdfAssociatedEventHandler : INotificationHandler<PrivatePdfAssociatedEvent>
{
    private readonly ILogger<PrivatePdfAssociatedEventHandler> _logger;

    public PrivatePdfAssociatedEventHandler(ILogger<PrivatePdfAssociatedEventHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task Handle(PrivatePdfAssociatedEvent notification, CancellationToken cancellationToken)
    {
        return HandleAsync(notification, cancellationToken);
    }

    internal Task HandleAsync(PrivatePdfAssociatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Intentional no-op: VectorDocument does not exist yet at upload time.
        // The actual auto-add is performed by AgentDocumentSyncOnReadyHandler
        // once indexing completes and a VectorDocument becomes available.
        _logger.LogDebug(
            "PrivatePdfAssociatedEvent received for PDF {PdfId}, game {GameId} — " +
            "deferred to AgentDocumentSyncOnReadyHandler after indexing",
            domainEvent.PdfDocumentId, domainEvent.GameId);

        return Task.CompletedTask;
    }
}

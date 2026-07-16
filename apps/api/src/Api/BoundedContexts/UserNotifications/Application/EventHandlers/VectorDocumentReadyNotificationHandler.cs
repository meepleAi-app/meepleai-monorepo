using Api.BoundedContexts.UserNotifications.Application.Constants;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.SharedKernel.Application.IntegrationEvents;
using MediatR;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles VectorDocumentReadyIntegrationEvent to send multi-channel notifications
/// when a user's PDF knowledge base is indexed and ready.
/// Issue #5237: Decoupled from KnowledgeBase - notifications handled via NotificationDispatcher.
/// </summary>
internal sealed class VectorDocumentReadyNotificationHandler
    : INotificationHandler<VectorDocumentReadyIntegrationEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<VectorDocumentReadyNotificationHandler> _logger;

    public VectorDocumentReadyNotificationHandler(
        INotificationDispatcher dispatcher,
        ILogger<VectorDocumentReadyNotificationHandler> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task Handle(
        VectorDocumentReadyIntegrationEvent evt,
        CancellationToken cancellationToken)
    {
        var userId = evt.UploadedByUserId;
        var fileName = evt.FileName;
        var agentLink = NotificationRoutes.LibraryAgent(evt.GameId);
        var chunkCount = evt.ChunkCount;

        await _dispatcher.DispatchAsync(new NotificationMessage
        {
            Type = NotificationType.DocumentReady,
            RecipientUserId = userId,
            Payload = new PdfProcessingPayload(
                Guid.Empty,
                fileName,
                $"Indexed ({chunkCount} chunks)"),
            DeepLinkPath = agentLink,
            // Issue #1937 / CF-1: propagate event id for dispatcher dedup
            SourceEventId = evt.EventId
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Dispatched KB ready notification for user {UserId}, game {GameId}, chunks: {ChunkCount}",
            userId,
            evt.GameId,
            chunkCount);
    }
}

using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.SharedKernel.Application.IntegrationEvents;
using MediatR;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles VectorDocumentReadyIntegrationEvent for SharedGame PDFs.
/// When a SharedGame's PDF finishes indexing, sends an admin-specific notification
/// with a deep link to /admin/shared-games/{gameId} (instead of the generic /library link).
///
/// Runs in addition to VectorDocumentReadyNotificationHandler which sends the general user notification.
/// Only fires when the indexed PDF belongs to a SharedGame.
/// </summary>
internal sealed class SharedGameIndexingAdminNotificationHandler
    : INotificationHandler<VectorDocumentReadyIntegrationEvent>
{
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<SharedGameIndexingAdminNotificationHandler> _logger;

    public SharedGameIndexingAdminNotificationHandler(
        ISharedGameRepository sharedGameRepository,
        INotificationDispatcher dispatcher,
        ILogger<SharedGameIndexingAdminNotificationHandler> logger)
    {
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        VectorDocumentReadyIntegrationEvent evt,
        CancellationToken cancellationToken)
    {
        // Only handle events that have a valid (non-empty) GameId
        if (evt.GameId == Guid.Empty)
            return;

        // Check if this GameId belongs to a SharedGame
        var sharedGame = await _sharedGameRepository.GetByIdAsync(evt.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (sharedGame == null)
            return; // Not a SharedGame — handled by the generic handler

        _logger.LogInformation(
            "Sending admin indexing notification: Game={GameTitle} ({GameId}), Chunks={ChunkCount}, User={UserId}",
            sharedGame.Title, evt.GameId, evt.ChunkCount, evt.UploadedByUserId);

        await _dispatcher.DispatchAsync(new NotificationMessage
        {
            Type = NotificationType.DocumentReady,
            RecipientUserId = evt.UploadedByUserId,
            Payload = new PdfProcessingPayload(
                evt.PdfDocumentId,
                evt.FileName,
                $"Indicizzato ({evt.ChunkCount} chunk) — pronto per RAG"),
            DeepLinkPath = $"/admin/shared-games/{evt.GameId}",
            Metadata = new
            {
                gameId = evt.GameId,
                gameTitle = sharedGame.Title,
                chunkCount = evt.ChunkCount,
                pdfDocumentId = evt.PdfDocumentId,
                isSharedGame = true
            }
        }, cancellationToken).ConfigureAwait(false);
    }
}

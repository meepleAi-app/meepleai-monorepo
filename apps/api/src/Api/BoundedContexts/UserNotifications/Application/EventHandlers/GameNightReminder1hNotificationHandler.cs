using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles GameNightReminder1hEvent to send 1-hour reminders via NotificationDispatcher.
/// Only notifies accepted users.
/// Issue #44: Game night reminder notifications.
/// </summary>
internal sealed class GameNightReminder1hNotificationHandler : INotificationHandler<GameNightReminder1hEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly IGameNightEventRepository _gameNightEventRepository;
    private readonly ILogger<GameNightReminder1hNotificationHandler> _logger;

    public GameNightReminder1hNotificationHandler(
        INotificationDispatcher dispatcher,
        IGameNightEventRepository gameNightEventRepository,
        ILogger<GameNightReminder1hNotificationHandler> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _gameNightEventRepository = gameNightEventRepository ?? throw new ArgumentNullException(nameof(gameNightEventRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(GameNightReminder1hEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Load the full event with RSVPs
            var gameNightEvent = await _gameNightEventRepository.GetByIdAsync(notification.GameNightEventId, cancellationToken).ConfigureAwait(false);
            if (gameNightEvent == null)
            {
                _logger.LogWarning("GameNightEvent {EventId} not found for 1h reminder", notification.GameNightEventId);
                return;
            }

            var notifiedCount = 0;

            foreach (var rsvp in gameNightEvent.Rsvps)
            {
                // Only notify accepted users for 1h reminder
                if (rsvp.Status != RsvpStatus.Accepted)
                    continue;

                try
                {
                    await _dispatcher.DispatchAsync(new NotificationMessage
                    {
                        Type = NotificationType.GameNightReminder1h,
                        RecipientUserId = rsvp.UserId,
                        Payload = new GameNightPayload(
                            notification.GameNightEventId,
                            notification.Title,
                            notification.ScheduledAt.UtcDateTime,
                            string.Empty),
                        DeepLinkPath = $"/game-nights/{notification.GameNightEventId}"
                    }, cancellationToken).ConfigureAwait(false);
                    notifiedCount++;
                }
#pragma warning disable CA1031
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to dispatch 1h reminder to user {UserId} for event {EventId}",
                        rsvp.UserId, notification.GameNightEventId);
                }
#pragma warning restore CA1031
            }

            _logger.LogInformation(
                "Dispatched 1h reminder notifications to {Count} accepted users for event {EventId}",
                notifiedCount, notification.GameNightEventId);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle GameNightReminder1hEvent for event {EventId}",
                notification.GameNightEventId);
        }
#pragma warning restore CA1031
    }
}

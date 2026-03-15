using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles GameNightReminder24hEvent to send 24-hour reminders via NotificationDispatcher.
/// Notifies accepted and pending RSVPs.
/// Issue #44: Game night reminder notifications.
/// </summary>
internal sealed class GameNightReminder24hNotificationHandler : INotificationHandler<GameNightReminder24hEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly IGameNightEventRepository _gameNightEventRepository;
    private readonly ILogger<GameNightReminder24hNotificationHandler> _logger;

    public GameNightReminder24hNotificationHandler(
        INotificationDispatcher dispatcher,
        IGameNightEventRepository gameNightEventRepository,
        ILogger<GameNightReminder24hNotificationHandler> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _gameNightEventRepository = gameNightEventRepository ?? throw new ArgumentNullException(nameof(gameNightEventRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(GameNightReminder24hEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Load the full event with RSVPs
            var gameNightEvent = await _gameNightEventRepository.GetByIdAsync(notification.GameNightEventId, cancellationToken).ConfigureAwait(false);
            if (gameNightEvent == null)
            {
                _logger.LogWarning("GameNightEvent {EventId} not found for 24h reminder", notification.GameNightEventId);
                return;
            }

            foreach (var rsvp in gameNightEvent.Rsvps)
            {
                // Only notify Accepted and Pending users
                if (rsvp.Status == RsvpStatus.Declined)
                    continue;

                try
                {
                    await _dispatcher.DispatchAsync(new NotificationMessage
                    {
                        Type = NotificationType.GameNightReminder24h,
                        RecipientUserId = rsvp.UserId,
                        Payload = new GameNightPayload(
                            notification.GameNightEventId,
                            notification.Title,
                            notification.ScheduledAt.UtcDateTime,
                            string.Empty),
                        DeepLinkPath = $"/game-nights/{notification.GameNightEventId}"
                    }, cancellationToken).ConfigureAwait(false);
                }
#pragma warning disable CA1031
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to dispatch 24h reminder to user {UserId} for event {EventId}",
                        rsvp.UserId, notification.GameNightEventId);
                }
#pragma warning restore CA1031
            }

            _logger.LogInformation(
                "Dispatched 24h reminder notifications for event {EventId}",
                notification.GameNightEventId);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle GameNightReminder24hEvent for event {EventId}",
                notification.GameNightEventId);
        }
#pragma warning restore CA1031
    }
}

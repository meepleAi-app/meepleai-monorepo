using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles GameNightCancelledEvent to notify ALL invitees via NotificationDispatcher.
/// Issue #47: Game night cancellation notifications.
/// </summary>
internal sealed class GameNightCancelledNotificationHandler : INotificationHandler<GameNightCancelledEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly IUserRepository _userRepository;
    private readonly IGameNightEventRepository _gameNightEventRepository;
    private readonly ILogger<GameNightCancelledNotificationHandler> _logger;

    public GameNightCancelledNotificationHandler(
        INotificationDispatcher dispatcher,
        IUserRepository userRepository,
        IGameNightEventRepository gameNightEventRepository,
        ILogger<GameNightCancelledNotificationHandler> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _gameNightEventRepository = gameNightEventRepository ?? throw new ArgumentNullException(nameof(gameNightEventRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(GameNightCancelledEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Resolve organizer name
            var organizer = await _userRepository.GetByIdAsync(notification.OrganizerId, cancellationToken).ConfigureAwait(false);
            var organizerName = organizer?.DisplayName ?? "The organizer";

            // Load the full event to get ScheduledAt
            var gameNightEvent = await _gameNightEventRepository.GetByIdAsync(notification.GameNightEventId, cancellationToken).ConfigureAwait(false);
            var scheduledAt = gameNightEvent?.ScheduledAt ?? DateTimeOffset.UtcNow;

            foreach (var invitedUserId in notification.InvitedUserIds)
            {
                try
                {
                    await _dispatcher.DispatchAsync(new NotificationMessage
                    {
                        Type = NotificationType.GameNightCancelled,
                        RecipientUserId = invitedUserId,
                        Payload = new GameNightPayload(
                            notification.GameNightEventId,
                            notification.Title,
                            scheduledAt.UtcDateTime,
                            organizerName),
                        DeepLinkPath = $"/game-nights/{notification.GameNightEventId}"
                    }, cancellationToken).ConfigureAwait(false);
                }
#pragma warning disable CA1031
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to dispatch cancellation notification to user {UserId} for event {EventId}",
                        invitedUserId, notification.GameNightEventId);
                }
#pragma warning restore CA1031
            }

            _logger.LogInformation(
                "Dispatched game night cancelled notifications for event {EventId} to {Count} invitees",
                notification.GameNightEventId, notification.InvitedUserIds.Count);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle GameNightCancelledEvent for event {EventId}",
                notification.GameNightEventId);
        }
#pragma warning restore CA1031
    }
}

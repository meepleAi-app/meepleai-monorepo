using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles GameNightPublishedEvent to notify each invited user via NotificationDispatcher.
/// Issue #44/#47: Game night notification types.
/// </summary>
internal sealed class GameNightPublishedNotificationHandler : INotificationHandler<GameNightPublishedEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GameNightPublishedNotificationHandler> _logger;

    public GameNightPublishedNotificationHandler(
        INotificationDispatcher dispatcher,
        IUserRepository userRepository,
        ILogger<GameNightPublishedNotificationHandler> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(GameNightPublishedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Resolve organizer name
            var organizer = await _userRepository.GetByIdAsync(notification.OrganizerId, cancellationToken).ConfigureAwait(false);
            var organizerName = organizer?.DisplayName ?? "Someone";

            foreach (var invitedUserId in notification.InvitedUserIds)
            {
                try
                {
                    await _dispatcher.DispatchAsync(new NotificationMessage
                    {
                        Type = NotificationType.GameNightInvitation,
                        RecipientUserId = invitedUserId,
                        Payload = new GameNightPayload(
                            notification.GameNightEventId,
                            notification.Title,
                            notification.ScheduledAt.UtcDateTime,
                            organizerName),
                        DeepLinkPath = $"/game-nights/{notification.GameNightEventId}"
                    }, cancellationToken).ConfigureAwait(false);
                }
#pragma warning disable CA1031
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to dispatch game night invitation notification to user {UserId} for event {EventId}",
                        invitedUserId, notification.GameNightEventId);
                }
#pragma warning restore CA1031
            }

            _logger.LogInformation(
                "Dispatched game night published notifications for event {EventId} to {Count} invited users",
                notification.GameNightEventId, notification.InvitedUserIds.Count);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle GameNightPublishedEvent for event {EventId}",
                notification.GameNightEventId);
        }
#pragma warning restore CA1031
    }
}

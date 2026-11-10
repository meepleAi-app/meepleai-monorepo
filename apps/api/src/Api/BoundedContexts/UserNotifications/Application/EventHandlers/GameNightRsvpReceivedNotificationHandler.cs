using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.UserNotifications.Application.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles GameNightRsvpReceivedEvent to notify the organizer of a new RSVP response via NotificationDispatcher.
/// Issue #44/#47: Game night notification types. A guest invited by email answers through
/// <see cref="GameNightGuestRespondedNotificationHandler"/> (#4947); the message is the same.
/// </summary>
internal sealed class GameNightRsvpReceivedNotificationHandler : INotificationHandler<GameNightRsvpReceivedEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GameNightRsvpReceivedNotificationHandler> _logger;

    public GameNightRsvpReceivedNotificationHandler(
        INotificationDispatcher dispatcher,
        IUserRepository userRepository,
        ILogger<GameNightRsvpReceivedNotificationHandler> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(GameNightRsvpReceivedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Resolve the responding user's name
            var respondingUser = await _userRepository.GetByIdAsync(notification.UserId, cancellationToken).ConfigureAwait(false);
            var respondingUserName = respondingUser?.DisplayName ?? GameNightRsvpReceivedMessage.UnknownResponder;

            await _dispatcher.DispatchAsync(
                GameNightRsvpReceivedMessage.For(
                    organizerId: notification.OrganizerId,
                    gameNightId: notification.GameNightEventId,
                    responderName: respondingUserName,
                    status: notification.RsvpStatus,
                    sourceEventId: notification.EventId),
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Dispatched RSVP received notification for organizer {OrganizerId} - user {UserId} {Status} event {EventId}",
                notification.OrganizerId, notification.UserId, notification.RsvpStatus, notification.GameNightEventId);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle GameNightRsvpReceivedEvent for event {EventId}, user {UserId}",
                notification.GameNightEventId, notification.UserId);
        }
#pragma warning restore CA1031
    }
}

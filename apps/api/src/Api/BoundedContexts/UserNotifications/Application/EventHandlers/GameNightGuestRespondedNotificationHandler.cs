using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.UserNotifications.Application.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// #4947: tells the organizer that a guest invited by email answered. A member's answer raises
/// <c>GameNightRsvpReceivedEvent</c> on the aggregate and the organizer is notified; a guest's answer raises
/// <see cref="GameNightInvitationRespondedEvent"/> on the invitation, whose only handler sent the guest their
/// confirmation. The organizer heard nothing — not of the first answer from the link, not of a change of mind
/// from the app (#4921). Same notification type and text as for a member, so the organizer's feed reads as one.
/// </summary>
/// <remarks>
/// The event does not carry the organizer, so the night is read for it; the invitation is read for the name the
/// guest left (#1169), falling back to their account's display name and then to the address the organizer typed
/// (nothing the organizer does not already know). <c>SourceEventId</c> dedups the deliveries of one event (#1937,
/// #4841). Nothing is written on the invitation: a notification is not an email, and the dispatcher's dedup is the
/// marker.
/// </remarks>
internal sealed class GameNightGuestRespondedNotificationHandler : INotificationHandler<GameNightInvitationRespondedEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly IGameNightEventRepository _gameNights;
    private readonly IGameNightInvitationRepository _invitations;
    private readonly IUserRepository _users;
    private readonly ILogger<GameNightGuestRespondedNotificationHandler> _logger;

    public GameNightGuestRespondedNotificationHandler(
        INotificationDispatcher dispatcher,
        IGameNightEventRepository gameNights,
        IGameNightInvitationRepository invitations,
        IUserRepository users,
        ILogger<GameNightGuestRespondedNotificationHandler> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _gameNights = gameNights ?? throw new ArgumentNullException(nameof(gameNights));
        _invitations = invitations ?? throw new ArgumentNullException(nameof(invitations));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(GameNightInvitationRespondedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        RsvpStatus status;
        switch (notification.Status)
        {
            case GameNightInvitationStatus.Accepted:
                status = RsvpStatus.Accepted;
                break;
            case GameNightInvitationStatus.Declined:
                status = RsvpStatus.Declined;
                break;
            default:
                // The event is raised for answers only; anything else is not the organizer's news.
                return;
        }

        try
        {
            var gameNight = await _gameNights.GetByIdAsync(notification.GameNightId, cancellationToken).ConfigureAwait(false);
            if (gameNight is null)
            {
                _logger.LogWarning(
                    "Game night {GameNightId} not found for guest answer on invitation {InvitationId}: no organizer to notify",
                    notification.GameNightId, notification.GameNightInvitationId);
                return;
            }

            var responderName = await ResolveResponderNameAsync(notification, cancellationToken).ConfigureAwait(false);

            await _dispatcher.DispatchAsync(
                GameNightRsvpReceivedMessage.For(
                    organizerId: gameNight.OrganizerId,
                    gameNightId: gameNight.Id,
                    responderName: responderName,
                    status: status,
                    sourceEventId: notification.EventId),
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Dispatched guest RSVP notification for organizer {OrganizerId}: invitation {InvitationId} {Status} on night {GameNightId}",
                gameNight.OrganizerId, notification.GameNightInvitationId, status, gameNight.Id);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            // As the member handler does: a notification that fails must not fail the answer, nor send the event
            // back through the outbox to fail the same way.
            _logger.LogError(ex,
                "Failed to notify the organizer of guest answer on invitation {InvitationId} (night {GameNightId})",
                notification.GameNightInvitationId, notification.GameNightId);
        }
#pragma warning restore CA1031
    }

    private async Task<string> ResolveResponderNameAsync(GameNightInvitationRespondedEvent notification, CancellationToken cancellationToken)
    {
        var invitation = await _invitations.GetByIdAsync(notification.GameNightInvitationId, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(invitation?.RespondedByName))
        {
            return invitation.RespondedByName;
        }

        if (notification.RespondedByUserId is { } userId)
        {
            var user = await _users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(user?.DisplayName))
            {
                return user.DisplayName;
            }
        }

        return invitation?.Email ?? GameNightRsvpReceivedMessage.UnknownResponder;
    }
}

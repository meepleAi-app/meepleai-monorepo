using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.UserNotifications.Application.Constants;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// The «somebody answered your invitation» notification to the organizer, built the same way whether the answer
/// came from a member (an RSVP row, <see cref="GameNightRsvpReceivedNotificationHandler"/>) or from a guest invited
/// by email (<see cref="GameNightGuestRespondedNotificationHandler"/>, #4947). One text, one type, one deep link.
/// </summary>
internal static class GameNightRsvpReceivedMessage
{
    public const string UnknownResponder = "A user";

    public static NotificationMessage For(
        Guid organizerId,
        Guid gameNightId,
        string responderName,
        RsvpStatus status,
        Guid sourceEventId)
    {
        var statusText = status switch
        {
            RsvpStatus.Accepted => "accepted",
            RsvpStatus.Declined => "declined",
            RsvpStatus.Maybe => "responded maybe to",
            _ => "responded to"
        };

        return new NotificationMessage
        {
            Type = NotificationType.GameNightRsvpReceived,
            RecipientUserId = organizerId,
            Payload = new GenericPayload(
                "RSVP Received",
                $"{responderName} {statusText} your game night invitation"),
            DeepLinkPath = NotificationRoutes.GameNight(gameNightId),
            // Issue #1937 / CF-1: the event id dedups at the dispatcher. The same event reaches handlers more than
            // once (Hybrid dispatch publishes inline AND through the outbox; OutboxOnly redelivers a failed batch).
            SourceEventId = sourceEventId
        };
    }
}

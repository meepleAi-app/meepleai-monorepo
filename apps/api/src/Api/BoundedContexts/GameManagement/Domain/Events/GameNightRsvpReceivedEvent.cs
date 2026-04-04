using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a user responds to a game night invitation.
/// Issue #42: GameNightEvent + GameNightRsvp domain entities.
/// </summary>
internal sealed class GameNightRsvpReceivedEvent : DomainEventBase
{
    public Guid GameNightEventId { get; }
    public Guid UserId { get; }
    public RsvpStatus RsvpStatus { get; }
    public Guid OrganizerId { get; }

    public GameNightRsvpReceivedEvent(Guid gameNightEventId, Guid userId, RsvpStatus rsvpStatus, Guid organizerId)
    {
        GameNightEventId = gameNightEventId;
        UserId = userId;
        RsvpStatus = rsvpStatus;
        OrganizerId = organizerId;
    }
}

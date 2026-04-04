using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a game night is cancelled.
/// Issue #42: GameNightEvent + GameNightRsvp domain entities.
/// </summary>
internal sealed class GameNightCancelledEvent : DomainEventBase
{
    public Guid GameNightEventId { get; }
    public Guid OrganizerId { get; }
    public string Title { get; }
    public List<Guid> InvitedUserIds { get; }

    public GameNightCancelledEvent(Guid gameNightEventId, Guid organizerId, string title, List<Guid> invitedUserIds)
    {
        GameNightEventId = gameNightEventId;
        OrganizerId = organizerId;
        Title = title;
        InvitedUserIds = invitedUserIds;
    }
}

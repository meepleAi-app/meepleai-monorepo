using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a game night is published and invitations are sent.
/// Issue #42: GameNightEvent + GameNightRsvp domain entities.
/// </summary>
internal sealed class GameNightPublishedEvent : DomainEventBase
{
    public Guid GameNightEventId { get; }
    public Guid OrganizerId { get; }
    public string Title { get; }
    public DateTimeOffset ScheduledAt { get; }
    public List<Guid> InvitedUserIds { get; }

    public GameNightPublishedEvent(Guid gameNightEventId, Guid organizerId, string title, DateTimeOffset scheduledAt, List<Guid> invitedUserIds)
    {
        GameNightEventId = gameNightEventId;
        OrganizerId = organizerId;
        Title = title;
        ScheduledAt = scheduledAt;
        InvitedUserIds = invitedUserIds;
    }
}

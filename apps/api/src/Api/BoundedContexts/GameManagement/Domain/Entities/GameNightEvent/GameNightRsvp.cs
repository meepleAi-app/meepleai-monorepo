using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;

/// <summary>
/// Entity representing an RSVP to a game night event.
/// Owned by the GameNightEvent aggregate root.
/// Issue #42: GameNightEvent + GameNightRsvp domain entities.
/// </summary>
public sealed class GameNightRsvp
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }
    public RsvpStatus Status { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

#pragma warning disable CS8618
    private GameNightRsvp() { } // EF Core
#pragma warning restore CS8618

    internal static GameNightRsvp Create(Guid eventId, Guid userId)
    {
        if (eventId == Guid.Empty) throw new ArgumentException("EventId required", nameof(eventId));
        if (userId == Guid.Empty) throw new ArgumentException("UserId required", nameof(userId));

        return new GameNightRsvp
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            Status = RsvpStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Reconstitutes an RSVP from persistence data.
    /// </summary>
    internal static GameNightRsvp Reconstitute(
        Guid id, Guid eventId, Guid userId, RsvpStatus status,
        DateTimeOffset? respondedAt, DateTimeOffset createdAt)
    {
        return new GameNightRsvp
        {
            Id = id,
            EventId = eventId,
            UserId = userId,
            Status = status,
            RespondedAt = respondedAt,
            CreatedAt = createdAt
        };
    }

    public void Accept()
    {
        Status = RsvpStatus.Accepted;
        RespondedAt = DateTimeOffset.UtcNow;
    }

    public void Decline()
    {
        Status = RsvpStatus.Declined;
        RespondedAt = DateTimeOffset.UtcNow;
    }

    public void SetMaybe()
    {
        Status = RsvpStatus.Maybe;
        RespondedAt = DateTimeOffset.UtcNow;
    }
}

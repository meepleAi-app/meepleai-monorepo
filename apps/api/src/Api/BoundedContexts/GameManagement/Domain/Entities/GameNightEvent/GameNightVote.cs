namespace Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;

/// <summary>
/// Entity representing a single approval vote for a candidate game on a game night.
/// Owned by the <see cref="GameNightEvent"/> aggregate root — Issue #2700.
///
/// Approval model: a confirmed participant casts at most one vote per candidate game
/// (uniqueness enforced by the aggregate + a DB unique index on
/// (event, voter, candidate)).
/// </summary>
public sealed class GameNightVote
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid VoterUserId { get; private set; }
    public Guid CandidateGameId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

#pragma warning disable CS8618
    private GameNightVote() { } // EF Core
#pragma warning restore CS8618

    internal static GameNightVote Create(Guid eventId, Guid voterUserId, Guid candidateGameId)
    {
        if (eventId == Guid.Empty) throw new ArgumentException("EventId required", nameof(eventId));
        if (voterUserId == Guid.Empty) throw new ArgumentException("VoterUserId required", nameof(voterUserId));
        if (candidateGameId == Guid.Empty) throw new ArgumentException("CandidateGameId required", nameof(candidateGameId));

        return new GameNightVote
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            VoterUserId = voterUserId,
            CandidateGameId = candidateGameId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Reconstitutes a vote from persistence data.
    /// </summary>
    internal static GameNightVote Reconstitute(
        Guid id, Guid eventId, Guid voterUserId, Guid candidateGameId, DateTimeOffset createdAt)
    {
        return new GameNightVote
        {
            Id = id,
            EventId = eventId,
            VoterUserId = voterUserId,
            CandidateGameId = candidateGameId,
            CreatedAt = createdAt
        };
    }
}

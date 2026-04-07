using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;

/// <summary>
/// Links a GameNightEvent to a Session, tracking play order and per-game outcome
/// within a multi-game evening. Each game night can have 1-5 sessions in sequence.
/// </summary>
internal sealed class GameNightSession
{
    public Guid Id { get; private set; }
    public Guid GameNightEventId { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid GameId { get; private set; }
    public string GameTitle { get; private set; } = string.Empty;
    public int PlayOrder { get; private set; }
    public GameNightSessionStatus Status { get; private set; }
    public Guid? WinnerId { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private GameNightSession() { }

    public static GameNightSession Create(
        Guid gameNightEventId, Guid sessionId, Guid gameId, string gameTitle, int playOrder)
    {
        if (gameNightEventId == Guid.Empty)
            throw new ArgumentException("GameNightEventId cannot be empty.", nameof(gameNightEventId));
        if (sessionId == Guid.Empty)
            throw new ArgumentException("SessionId cannot be empty.", nameof(sessionId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty.", nameof(gameId));
        if (string.IsNullOrWhiteSpace(gameTitle))
            throw new ArgumentException("GameTitle cannot be empty.", nameof(gameTitle));
        if (playOrder < 1)
            throw new ArgumentException("PlayOrder must be >= 1.", nameof(playOrder));

        return new GameNightSession
        {
            Id = Guid.NewGuid(),
            GameNightEventId = gameNightEventId,
            SessionId = sessionId,
            GameId = gameId,
            GameTitle = gameTitle.Trim(),
            PlayOrder = playOrder,
            Status = GameNightSessionStatus.Pending
        };
    }

    public static GameNightSession Reconstitute(
        Guid id, Guid gameNightEventId, Guid sessionId, Guid gameId,
        string gameTitle, int playOrder, GameNightSessionStatus status,
        Guid? winnerId, DateTimeOffset? startedAt, DateTimeOffset? completedAt)
    {
        return new GameNightSession
        {
            Id = id,
            GameNightEventId = gameNightEventId,
            SessionId = sessionId,
            GameId = gameId,
            GameTitle = gameTitle,
            PlayOrder = playOrder,
            Status = status,
            WinnerId = winnerId,
            StartedAt = startedAt,
            CompletedAt = completedAt
        };
    }

    internal void Start()
    {
        if (Status != GameNightSessionStatus.Pending)
            throw new InvalidOperationException($"Cannot start session in {Status} status.");
        Status = GameNightSessionStatus.InProgress;
        StartedAt = DateTimeOffset.UtcNow;
    }

    internal void Complete(Guid? winnerId)
    {
        if (Status != GameNightSessionStatus.InProgress)
            throw new InvalidOperationException($"Cannot complete session in {Status} status.");
        Status = GameNightSessionStatus.Completed;
        WinnerId = winnerId;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    internal void Skip()
    {
        if (Status != GameNightSessionStatus.Pending)
            throw new InvalidOperationException($"Cannot skip session in {Status} status.");
        Status = GameNightSessionStatus.Skipped;
    }
}

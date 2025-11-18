using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// GameSession aggregate root representing an active or completed game play session.
/// </summary>
public sealed class GameSession : AggregateRoot<Guid>
{
    public Guid GameId { get; private set; }
    public SessionStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? Notes { get; private set; }

    // Players participating in this session
    private readonly List<SessionPlayer> _players = new();
    public IReadOnlyList<SessionPlayer> Players => _players.AsReadOnly();

    // Winner tracking (nullable, set when session completes)
    public string? WinnerName { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private GameSession() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new game session in Setup status.
    /// </summary>
    public GameSession(
        Guid id,
        Guid gameId,
        IEnumerable<SessionPlayer> players) : base(id)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty", nameof(gameId));

        var playerList = players?.ToList() ?? throw new ArgumentNullException(nameof(players));

        if (playerList.Count == 0)
            throw new ArgumentException("Session must have at least one player", nameof(players));

        if (playerList.Count > 100)
            throw new ArgumentException("Session cannot have more than 100 players", nameof(players));

        GameId = gameId;
        Status = SessionStatus.Setup;
        StartedAt = DateTime.UtcNow;
        _players.AddRange(playerList);

        AddDomainEvent(new GameSessionCreatedEvent(id, gameId, playerList.Count));
    }

    /// <summary>
    /// Starts the game session (moves from Setup to InProgress).
    /// </summary>
    public void Start()
    {
        if (Status != SessionStatus.Setup)
            throw new InvalidOperationException($"Cannot start session in {Status} status. Session must be in Setup.");

        Status = SessionStatus.InProgress;

        AddDomainEvent(new GameSessionStartedEvent(Id, GameId, DateTime.UtcNow));
    }

    /// <summary>
    /// Pauses the game session (moves from InProgress to Paused).
    /// </summary>
    public void Pause()
    {
        if (Status != SessionStatus.InProgress)
            throw new InvalidOperationException($"Cannot pause session in {Status} status. Session must be InProgress.");

        Status = SessionStatus.Paused;

        AddDomainEvent(new GameSessionPausedEvent(Id, DateTime.UtcNow));
    }

    /// <summary>
    /// Resumes the game session (moves from Paused to InProgress).
    /// </summary>
    public void Resume()
    {
        if (Status != SessionStatus.Paused)
            throw new InvalidOperationException($"Cannot resume session in {Status} status. Session must be Paused.");

        Status = SessionStatus.InProgress;

        AddDomainEvent(new GameSessionResumedEvent(Id, DateTime.UtcNow));
    }

    /// <summary>
    /// Completes the session with optional winner.
    /// Can be completed from InProgress or Paused status.
    /// </summary>
    public void Complete(string? winnerName = null)
    {
        if (Status != SessionStatus.InProgress && Status != SessionStatus.Paused)
            throw new InvalidOperationException($"Cannot complete session in {Status} status. Session must be InProgress or Paused.");

        // Validate winner name length (consistent with SessionPlayer validation)
        if (winnerName != null)
        {
            var trimmed = winnerName.Trim();
            if (trimmed.Length > 50)
                throw new ValidationException("Winner name cannot exceed 50 characters");
            WinnerName = trimmed;
        }
        else
        {
            WinnerName = null;
        }

        Status = SessionStatus.Completed;
        CompletedAt = DateTime.UtcNow;

        AddDomainEvent(new GameSessionCompletedEvent(Id, GameId, DateTime.UtcNow, Duration));
    }

    /// <summary>
    /// Abandons the session (player quit, game interrupted).
    /// </summary>
    public void Abandon(string? reason = null)
    {
        if (Status.IsFinished)
            throw new InvalidOperationException($"Cannot abandon finished session (status: {Status})");

        Status = SessionStatus.Abandoned;
        CompletedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(reason))
        {
            Notes = Notes != null ? $"{Notes}\nAbandoned: {reason}" : $"Abandoned: {reason}";
        }

        AddDomainEvent(new GameSessionAbandonedEvent(Id, DateTime.UtcNow, reason));
    }

    /// <summary>
    /// Adds a player to the session.
    /// Can only add players when session is not finished (Setup, InProgress, or Paused).
    /// </summary>
    public void AddPlayer(SessionPlayer player)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));

        if (Status.IsFinished)
            throw new InvalidOperationException($"Cannot add player to finished session (status: {Status})");

        if (_players.Count >= 100)
            throw new InvalidOperationException("Session cannot have more than 100 players");

        if (HasPlayer(player.PlayerName))
            throw new InvalidOperationException($"Player '{player.PlayerName}' is already in this session");

        _players.Add(player);

        AddDomainEvent(new PlayerAddedToSessionEvent(Id, player.PlayerName, _players.Count));
    }

    /// <summary>
    /// Adds notes to the session.
    /// </summary>
    public void AddNotes(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return;

        var trimmed = notes.Trim();
        Notes = Notes != null ? $"{Notes}\n{trimmed}" : trimmed;
    }

    /// <summary>
    /// Gets the duration of the session.
    /// </summary>
    public TimeSpan Duration =>
        CompletedAt.HasValue
            ? CompletedAt.Value - StartedAt
            : DateTime.UtcNow - StartedAt;

    /// <summary>
    /// Convenience accessor for duration expressed in minutes.
    /// </summary>
    public double DurationMinutes => Math.Max(0, Duration.TotalMinutes);

    /// <summary>
    /// Gets total player count.
    /// </summary>
    public int PlayerCount => _players.Count;

    /// <summary>
    /// Checks if a specific player participated.
    /// </summary>
    public bool HasPlayer(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            return false;

        var normalized = playerName.Trim().ToLowerInvariant();
        return _players.Any(p => p.PlayerName.ToLowerInvariant() == normalized);
    }
}

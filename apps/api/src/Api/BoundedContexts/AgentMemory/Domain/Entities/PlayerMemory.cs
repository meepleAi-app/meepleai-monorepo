using Api.BoundedContexts.AgentMemory.Domain.Models;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.AgentMemory.Domain.Entities;

/// <summary>
/// Aggregate root tracking a player's game statistics. Can represent a registered user or a guest,
/// with support for guest-to-user claim.
/// </summary>
internal sealed class PlayerMemory : AggregateRoot<Guid>
{
    public Guid? UserId { get; private set; }
    public string? GuestName { get; private set; }
    public Guid? GroupId { get; private set; }

    private readonly List<PlayerGameStats> _gameStats = new();
    public IReadOnlyList<PlayerGameStats> GameStats => _gameStats.AsReadOnly();

    public DateTime? ClaimedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Last domain event id processed against this aggregate (issue #1939 / CF-3).
    /// When set and equal to the incoming event id, handlers MUST early-exit to avoid
    /// double-incrementing counters / stats on a retried/rolled-back event handler.
    /// </summary>
    public Guid? LastProcessedEventId { get; private set; }

    private readonly ProcessedSessions _processedSessions = new();

    /// <summary>The sessions already counted in this player's stats (#4839, option C), oldest first.</summary>
    public IReadOnlyList<Guid> ProcessedSessionIds => _processedSessions.Ids;

    /// <summary>EF Core constructor.</summary>
    private PlayerMemory() { }

    public static PlayerMemory CreateForUser(Guid userId, Guid? groupId = null)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId cannot be empty.", nameof(userId));

        return new PlayerMemory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GroupId = groupId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static PlayerMemory CreateForGuest(string guestName, Guid? groupId = null)
    {
        if (string.IsNullOrWhiteSpace(guestName))
            throw new ArgumentException("Guest name cannot be empty.", nameof(guestName));

        return new PlayerMemory
        {
            Id = Guid.NewGuid(),
            GuestName = guestName,
            GroupId = groupId,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Ricostituisce una memoria letta dal database, così com'è (#4552).
    /// </summary>
    /// <remarks>
    /// Solo per il repository. <see cref="CreateForUser"/> e <see cref="CreateForGuest"/> sono le factory di una
    /// memoria nuova, e ciascuna conosce uno solo dei due lati: la memoria di un ospite reclamato ha sia l'utente
    /// sia il nome dell'ospite. Ricostruita con <see cref="CreateForUser"/> perdeva il nome, e il primo
    /// aggiornamento delle statistiche lo scriveva a NULL.
    /// </remarks>
    internal static PlayerMemory Reconstitute(
        Guid id,
        Guid? userId,
        string? guestName,
        Guid? groupId,
        DateTime? claimedAt,
        DateTime createdAt,
        Guid? lastProcessedEventId,
        IEnumerable<PlayerGameStats> gameStats,
        IEnumerable<Guid>? processedSessionIds = null)
    {
        ArgumentNullException.ThrowIfNull(gameStats);

        var memory = new PlayerMemory
        {
            Id = id,
            UserId = userId,
            GuestName = guestName,
            GroupId = groupId,
            ClaimedAt = claimedAt,
            CreatedAt = createdAt,
            LastProcessedEventId = lastProcessedEventId,
        };
        memory._gameStats.AddRange(gameStats);
        foreach (var sessionId in new ProcessedSessions(processedSessionIds).Ids)
            memory._processedSessions.Add(sessionId);
        return memory;
    }

    public void UpdateGameStats(Guid gameId, bool won, int? score = null)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty.", nameof(gameId));

        var existing = _gameStats.Find(s => s.GameId == gameId);
        if (existing != null)
        {
            existing.RecordPlay(won, score);
        }
        else
        {
            _gameStats.Add(PlayerGameStats.Create(gameId, won, score));
        }
    }

    /// <summary>
    /// <c>true</c> when the result of this session has already been counted for this player, whatever event or
    /// delivery counted it (#4839, option C).
    /// </summary>
    public bool HasProcessedSession(Guid sessionId) => _processedSessions.Contains(sessionId);

    /// <summary>
    /// Records which domain event, and which session, have just been processed against this aggregate
    /// (issue #1939 / CF-3, #4839). Used by handlers to short-circuit on re-dispatch.
    /// </summary>
    public void MarkProcessed(Guid eventId, Guid sessionId)
    {
        LastProcessedEventId = eventId;
        _processedSessions.Add(sessionId);
    }
}

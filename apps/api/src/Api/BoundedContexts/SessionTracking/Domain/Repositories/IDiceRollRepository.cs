using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.BoundedContexts.SessionTracking.Domain.Repositories;

/// <summary>
/// Repository interface for DiceRoll aggregate.
/// </summary>
public interface IDiceRollRepository
{
    /// <summary>
    /// Gets a dice roll by ID.
    /// </summary>
    Task<DiceRoll?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets all dice rolls for a session.
    /// </summary>
    Task<IEnumerable<DiceRoll>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Gets recent dice rolls for a session (for history panel).
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    /// <param name="limit">Maximum number of rolls to return.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IEnumerable<DiceRoll>> GetRecentBySessionIdAsync(Guid sessionId, int limit = 20, CancellationToken ct = default);

    /// <summary>
    /// Gets dice rolls by participant in a session.
    /// </summary>
    Task<IEnumerable<DiceRoll>> GetByParticipantAsync(Guid sessionId, Guid participantId, CancellationToken ct = default);

    /// <summary>
    /// Adds a new dice roll.
    /// </summary>
    Task AddAsync(DiceRoll diceRoll, CancellationToken ct = default);
}

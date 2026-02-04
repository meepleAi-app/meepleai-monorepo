using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for StrategyPattern persistence.
/// Issue #3493: PostgreSQL Schema Extensions for Multi-Agent System.
/// </summary>
internal interface IStrategyPatternRepository
{
    /// <summary>
    /// Gets strategy patterns by game and phase.
    /// </summary>
    Task<List<StrategyPattern>> GetByGameAndPhaseAsync(
        Guid gameId,
        string phase,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets top-rated strategy patterns for a game, ordered by score descending.
    /// </summary>
    Task<List<StrategyPattern>> GetTopRatedByGameIdAsync(
        Guid gameId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of strategy patterns for a game.
    /// </summary>
    Task<int> CountByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a pattern by ID.
    /// </summary>
    Task<StrategyPattern?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new strategy pattern.
    /// </summary>
    Task AddAsync(StrategyPattern pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing pattern.
    /// </summary>
    Task UpdateAsync(StrategyPattern pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a pattern by ID.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

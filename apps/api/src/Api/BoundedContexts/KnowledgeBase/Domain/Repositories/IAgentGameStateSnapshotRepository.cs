using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for AgentGameStateSnapshot persistence.
/// Issue #3493: PostgreSQL Schema Extensions for Multi-Agent System.
/// </summary>
internal interface IAgentGameStateSnapshotRepository
{
    /// <summary>
    /// Gets the latest game state snapshot for a game.
    /// </summary>
    Task<AgentGameStateSnapshot?> GetLatestByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets game state snapshots by game ID, ordered by turn number descending.
    /// </summary>
    Task<List<AgentGameStateSnapshot>> GetByGameIdAsync(
        Guid gameId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of snapshots for a game.
    /// </summary>
    Task<int> CountByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a snapshot by ID.
    /// </summary>
    Task<AgentGameStateSnapshot?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new game state snapshot.
    /// </summary>
    Task AddAsync(AgentGameStateSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing snapshot.
    /// </summary>
    Task UpdateAsync(AgentGameStateSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes snapshots older than the specified date.
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);
}

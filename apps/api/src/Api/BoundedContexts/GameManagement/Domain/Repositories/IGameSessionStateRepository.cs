using Api.BoundedContexts.GameManagement.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

/// <summary>
/// Repository interface for GameSessionState aggregate.
/// </summary>
internal interface IGameSessionStateRepository
{
    /// <summary>
    /// Adds a new game session state.
    /// </summary>
    Task AddAsync(GameSessionState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets game session state by ID.
    /// </summary>
    Task<GameSessionState?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets game session state by session ID (one-to-one relationship).
    /// </summary>
    Task<GameSessionState?> GetBySessionIdAsync(Guid gameSessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing game session state.
    /// </summary>
    Task UpdateAsync(GameSessionState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a session has state initialized.
    /// </summary>
    Task<bool> ExistsBySessionIdAsync(Guid gameSessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a game session state and all snapshots.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

/// <summary>
/// Repository interface for GameSession aggregate.
/// </summary>
internal interface IGameSessionRepository : IRepository<GameSession, Guid>
{
    /// <summary>
    /// Finds active sessions for a specific game.
    /// </summary>
    Task<IReadOnlyList<GameSession>> FindActiveByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all sessions for a specific game.
    /// </summary>
    Task<IReadOnlyList<GameSession>> FindByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds sessions by player name.
    /// </summary>
    Task<IReadOnlyList<GameSession>> FindByPlayerNameAsync(string playerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all active sessions (Setup, InProgress, Paused) with pagination.
    /// </summary>
    Task<IReadOnlyList<GameSession>> FindActiveAsync(int? limit = null, int? offset = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts all active sessions.
    /// Issue #2755: Required for paginated response.
    /// </summary>
    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds session history (Completed, Abandoned) with filters and pagination.
    /// </summary>
    Task<IReadOnlyList<GameSession>> FindHistoryAsync(
        Guid? gameId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts active sessions (Setup, InProgress, Paused) for a specific user.
    /// Issue #3070: Required for session quota enforcement.
    /// </summary>
    /// <param name="userId">The user ID to count sessions for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of active sessions for the user</returns>
    Task<int> CountActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds oldest active sessions for a specific user, ordered by StartedAt ascending.
    /// Issue #3671: Required for automatic session termination when quota exceeded.
    /// </summary>
    /// <param name="userId">The user ID to find sessions for</param>
    /// <param name="limit">Maximum number of sessions to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of oldest active sessions (tracked for updates, not AsNoTracking)</returns>
    Task<IReadOnlyList<GameSession>> FindOldestActiveByUserIdAsync(Guid userId, int limit, CancellationToken cancellationToken = default);
}

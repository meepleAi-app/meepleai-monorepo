using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Domain.Repositories;

/// <summary>
/// Repository interface for GameSuggestion aggregate.
/// Admin Invitation Flow: manages game suggestions for invited users.
/// </summary>
internal interface IGameSuggestionRepository : IRepository<GameSuggestion, Guid>
{
    /// <summary>
    /// Gets all game suggestions for a user.
    /// </summary>
    /// <param name="userId">The user ID to look up suggestions for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of game suggestions for the user</returns>
    Task<IReadOnlyList<GameSuggestion>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a suggestion already exists for a specific user and game combination.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="gameId">Game ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if a suggestion exists, false otherwise</returns>
    Task<bool> ExistsForUserAndGameAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default);
}

using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Domain.Repositories;

/// <summary>
/// Repository interface for PrivateGame aggregate.
/// Issue #3662: Phase 1 - Data Model &amp; Core Infrastructure for Private Games.
/// </summary>
internal interface IPrivateGameRepository : IRepository<PrivateGame, Guid>
{
    /// <summary>
    /// Gets all private games owned by a user.
    /// </summary>
    /// <param name="ownerId">The owner's user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of private games owned by the user</returns>
    Task<IReadOnlyList<PrivateGame>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a private game by its BGG ID for a specific owner.
    /// </summary>
    /// <param name="ownerId">The owner's user ID</param>
    /// <param name="bggId">The BoardGameGeek ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The private game if found, null otherwise</returns>
    Task<PrivateGame?> GetByOwnerAndBggIdAsync(Guid ownerId, int bggId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a private game with the given BGG ID exists for the owner.
    /// </summary>
    /// <param name="ownerId">The owner's user ID</param>
    /// <param name="bggId">The BoardGameGeek ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if exists, false otherwise</returns>
    Task<bool> ExistsByOwnerAndBggIdAsync(Guid ownerId, int bggId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the total number of private games owned by a user.
    /// </summary>
    /// <param name="ownerId">The owner's user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of private games</returns>
    Task<int> CountByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches private games by title for a specific owner.
    /// </summary>
    /// <param name="ownerId">The owner's user ID</param>
    /// <param name="searchTerm">The search term to match against titles</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of matching private games</returns>
    Task<IReadOnlyList<PrivateGame>> SearchByTitleAsync(Guid ownerId, string searchTerm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets private games including soft-deleted ones (for admin purposes).
    /// </summary>
    /// <param name="ownerId">The owner's user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all private games including deleted</returns>
    Task<IReadOnlyList<PrivateGame>> GetByOwnerIdWithDeletedAsync(Guid ownerId, CancellationToken cancellationToken = default);
}

using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Domain.Repositories;

/// <summary>
/// Repository interface for LibraryShareLink aggregate.
/// </summary>
internal interface ILibraryShareLinkRepository : IRepository<LibraryShareLink, Guid>
{
    /// <summary>
    /// Gets a share link by its share token.
    /// </summary>
    /// <param name="shareToken">The share token to look up</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The share link if found, null otherwise</returns>
    Task<LibraryShareLink?> GetByShareTokenAsync(string shareToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active share link for a user (non-revoked, non-expired).
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The active share link if exists, null otherwise</returns>
    Task<LibraryShareLink?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all share links for a user (including revoked/expired).
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of share links</returns>
    Task<IReadOnlyList<LibraryShareLink>> GetAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts share links created by user in the last 24 hours (for rate limiting).
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of share links created in last 24 hours</returns>
    Task<int> CountRecentByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}

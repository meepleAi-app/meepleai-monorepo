using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for UserBadge entity operations.
/// </summary>
public interface IUserBadgeRepository
{
    /// <summary>
    /// Adds a new user badge to the repository.
    /// </summary>
    /// <param name="userBadge">The user badge to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(UserBadge userBadge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a revocation or a reinstatement of an existing user badge, and collects its domain events
    /// (#5116). Until then <see cref="UserBadge.Revoke"/> changed only the object in memory.
    /// </summary>
    /// <param name="userBadge">The user badge, read from this repository.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(UserBadge userBadge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all badge IDs that a user currently has (active only).
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HashSet of badge IDs the user currently has.</returns>
    Task<HashSet<Guid>> GetBadgeIdsByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all user badges for a specific user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="includeHidden">Whether to include hidden badges.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of user badges.</returns>
    Task<List<UserBadge>> GetByUserIdAsync(
        Guid userId,
        bool includeHidden = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves users who have a specific badge by badge code.
    /// </summary>
    /// <param name="badgeCode">The badge code (e.g., "TOP_CONTRIBUTOR").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of user badges for the specified badge code.</returns>
    Task<List<UserBadge>> GetUsersByBadgeCodeAsync(
        string badgeCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific user badge by user ID and badge ID, revoked or not: the unique (UserId, BadgeId)
    /// index allows one row per pair, and a revoked one is given back by reinstating it (#5116).
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="badgeId">The badge ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user badge if found, otherwise null.</returns>
    Task<UserBadge?> GetByUserAndBadgeAsync(
        Guid userId,
        Guid badgeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the top N badges for a specific user, ordered by tier and earned date.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="count">Maximum number of badges to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of top user badges.</returns>
    Task<List<UserBadge>> GetTopBadgesByUserAsync(
        Guid userId,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all distinct user IDs who have earned at least one badge.
    /// Used for batch badge recalculation operations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of distinct user IDs.</returns>
    Task<List<Guid>> GetAllDistinctUserIdsAsync(CancellationToken cancellationToken = default);
}

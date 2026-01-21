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
    /// Retrieves a specific user badge by user ID and badge ID.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="badgeId">The badge ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user badge if found, otherwise null.</returns>
    Task<UserBadge?> GetByUserAndBadgeAsync(
        Guid userId,
        Guid badgeId,
        CancellationToken cancellationToken = default);
}

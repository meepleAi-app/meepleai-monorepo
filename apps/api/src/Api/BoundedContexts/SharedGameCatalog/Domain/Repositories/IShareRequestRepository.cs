using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for ShareRequest aggregate root.
/// </summary>
public interface IShareRequestRepository
{
    /// <summary>
    /// Adds a new share request to the repository.
    /// </summary>
    /// <param name="request">The share request to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(ShareRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a share request by its ID.
    /// </summary>
    /// <param name="id">The share request ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The share request if found; otherwise null.</returns>
    Task<ShareRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a share request by ID with tracking enabled for updates.
    /// </summary>
    /// <param name="id">The share request ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The share request if found; otherwise null.</returns>
    Task<ShareRequest?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing share request.
    /// </summary>
    /// <param name="request">The share request to update.</param>
    void Update(ShareRequest request);

    /// <summary>
    /// Checks if a pending request exists for the given user and source game.
    /// Used to enforce the invariant: no duplicate pending requests per user/game.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="sourceGameId">The source game ID from user's library.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a pending request exists; otherwise false.</returns>
    Task<bool> HasPendingRequestAsync(
        Guid userId,
        Guid sourceGameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all share requests for a specific user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of share requests for the user.</returns>
    Task<IReadOnlyCollection<ShareRequest>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets share requests by status.
    /// </summary>
    /// <param name="status">The status to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of share requests with the specified status.</returns>
    Task<IReadOnlyCollection<ShareRequest>> GetByStatusAsync(
        ShareRequestStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets share requests that are currently being reviewed by a specific admin.
    /// </summary>
    /// <param name="adminId">The admin ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of share requests being reviewed by the admin.</returns>
    Task<IReadOnlyCollection<ShareRequest>> GetByReviewingAdminAsync(
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets share requests that have been in review longer than the specified duration.
    /// Used for automatic lock release.
    /// </summary>
    /// <param name="duration">The duration threshold.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of stale review requests.</returns>
    Task<IReadOnlyCollection<ShareRequest>> GetStaleReviewsAsync(
        TimeSpan duration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the total number of approved share requests for a user.
    /// Used for badge evaluation (contribution count requirements).
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of approved share requests.</returns>
    Task<int> CountApprovedByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent resolved share requests for a user (approved or rejected).
    /// Used for quality streak badge evaluation.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="count">Number of recent requests to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of recent resolved share requests, ordered by resolution date descending.</returns>
    Task<List<ShareRequest>> GetRecentResolvedByUserAsync(
        Guid userId,
        int count,
        CancellationToken cancellationToken = default);
}

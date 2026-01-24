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

    // ===== Rate Limiting Support Methods (Issue #2730) =====

    /// <summary>
    /// Counts the number of pending share requests for a user.
    /// Used for rate limit evaluation (max pending requests check).
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of pending share requests.</returns>
    Task<int> CountPendingByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the number of share requests created by a user in the current calendar month (UTC).
    /// Used for rate limit evaluation (monthly request limit check).
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of share requests created this month.</returns>
    Task<int> CountThisMonthByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the date/time when the user's most recent request was rejected.
    /// Used for rate limit cooldown calculation.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rejection date/time if any rejected requests exist; otherwise null.</returns>
    Task<DateTime?> GetLastRejectionDateAsync(Guid userId, CancellationToken cancellationToken = default);

    // ===== Admin Dashboard Support Methods (Issue #2740) =====

    /// <summary>
    /// Counts all pending share requests across all users.
    /// Used for admin dashboard badge display.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total count of pending requests.</returns>
    Task<int> CountPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts all in-review share requests across all users.
    /// Used for admin dashboard badge display.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total count of in-review requests.</returns>
    Task<int> CountInReviewAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending share requests older than the specified threshold.
    /// Used for stale request warning job.
    /// </summary>
    /// <param name="threshold">DateTime threshold for staleness check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of stale pending requests.</returns>
    Task<IReadOnlyCollection<ShareRequest>> GetPendingOlderThanAsync(
        DateTime threshold,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregated statistics for pending share requests.
    /// Used for daily admin digest email.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated pending request statistics.</returns>
    Task<PendingShareRequestStats> GetPendingStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics for pending share requests.
/// Used in admin digest emails and dashboard.
/// </summary>
public record PendingShareRequestStats
{
    /// <summary>
    /// Total number of pending requests.
    /// </summary>
    public int TotalPending { get; init; }

    /// <summary>
    /// Number of requests created today (UTC).
    /// </summary>
    public int CreatedToday { get; init; }

    /// <summary>
    /// Age of the oldest pending request.
    /// </summary>
    public TimeSpan OldestPendingAge { get; init; }

    /// <summary>
    /// Breakdown of pending requests by contribution type.
    /// </summary>
    public Dictionary<string, int> ByType { get; init; } = new(StringComparer.Ordinal);
}

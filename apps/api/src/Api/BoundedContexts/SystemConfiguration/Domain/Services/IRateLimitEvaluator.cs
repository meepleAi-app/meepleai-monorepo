using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Services;

/// <summary>
/// Domain service interface for evaluating user rate limits for share requests.
/// Combines tier-based limits with user-specific overrides to determine effective limits.
/// </summary>
public interface IRateLimitEvaluator
{
    /// <summary>
    /// Gets the current rate limit status for a user.
    /// Includes effective limits, current usage, and whether they can create a request.
    /// </summary>
    /// <param name="userId">The user to get status for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user's current rate limit status.</returns>
    Task<RateLimitStatus> GetUserStatusAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user can create a new share request.
    /// </summary>
    /// <param name="userId">The user to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user can create a request, false otherwise.</returns>
    Task<bool> CanUserCreateRequestAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a user's share request was rejected.
    /// This starts the cooldown timer for that user.
    /// </summary>
    /// <param name="userId">The user whose request was rejected.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordRejectionAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about a user's share request activity.
/// Used by the rate limit evaluator to calculate status.
/// </summary>
/// <param name="UserId">The user these stats are for.</param>
/// <param name="Tier">The user's current tier level.</param>
/// <param name="PendingRequestCount">Number of pending share requests.</param>
/// <param name="CurrentMonthRequestCount">Number of requests made this month.</param>
/// <param name="LastRejectionAt">When the user was last rejected (if applicable).</param>
public record RateLimitUserStats(
    Guid UserId,
    UserTier Tier,
    int PendingRequestCount,
    int CurrentMonthRequestCount,
    DateTime? LastRejectionAt);

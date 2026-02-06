namespace Api.BoundedContexts.Administration.Domain.Services;

/// <summary>
/// Service for tracking token usage and enforcing limits (Issue #3786)
/// </summary>
public interface ITokenTrackingService
{
    /// <summary>
    /// Track token usage for a user
    /// Returns remaining tokens and whether limit was exceeded
    /// </summary>
    Task<(bool exceeded, int remaining)> TrackUsageAsync(Guid userId, int tokensConsumed, decimal cost, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check current limits without tracking usage
    /// Returns whether user is blocked and remaining tokens
    /// </summary>
    Task<(bool exceeded, int remaining, bool isBlocked)> CheckLimitsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current usage statistics for a user
    /// </summary>
    Task<(int tokensUsed, int messagesCount, decimal cost, int limit, double percentageUsed)> GetUserUsageAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset monthly usage for a user (admin action or scheduled job)
    /// </summary>
    Task ResetMonthlyUsageAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset monthly usage for all users (scheduled job - month start)
    /// </summary>
    Task ResetAllUsersMonthlyUsageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users nearing their limits (for warning notifications)
    /// </summary>
    Task<List<(Guid userId, int tokensUsed, int limit, double percentageUsed)>> GetUsersNearingLimitAsync(int thresholdPercentage = 80, CancellationToken cancellationToken = default);
}

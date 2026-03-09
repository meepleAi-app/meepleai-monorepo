namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Budget isolation and rate limiting for A/B testing playground.
/// Issue #5505: Separate daily budget, per-role rate limits, response caching.
/// </summary>
public interface IAbTestBudgetService
{
    /// <summary>
    /// Check if the daily A/B testing budget has been exceeded.
    /// </summary>
    Task<bool> HasBudgetRemainingAsync(CancellationToken ct = default);

    /// <summary>
    /// Record cost for an A/B test request against the daily budget.
    /// </summary>
    Task RecordTestCostAsync(decimal costUsd, CancellationToken ct = default);

    /// <summary>
    /// Get current daily spend for A/B testing.
    /// </summary>
    Task<decimal> GetDailySpendAsync(CancellationToken ct = default);

    /// <summary>
    /// Check if a user has remaining rate limit for A/B tests today.
    /// </summary>
    /// <param name="userId">The user performing the test.</param>
    /// <param name="isAdmin">True for admin (200/day), false for editor (50/day).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> HasRateLimitRemainingAsync(Guid userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>
    /// Record a test execution against the user's daily rate limit.
    /// </summary>
    Task RecordTestExecutionAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get the number of tests a user has run today.
    /// </summary>
    Task<int> GetUserTestCountTodayAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get a cached LLM response for a query+model combination, or null if not cached.
    /// Responses are cached for 24 hours to avoid duplicate costs.
    /// </summary>
    Task<string?> GetCachedResponseAsync(string query, string modelId, CancellationToken ct = default);

    /// <summary>
    /// Cache an LLM response for a query+model combination (24h TTL).
    /// </summary>
    Task CacheResponseAsync(string query, string modelId, string response, CancellationToken ct = default);
}

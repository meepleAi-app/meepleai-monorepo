namespace Api.Services;

/// <summary>
/// Service for retrieving resource-level metrics (tokens, database, cache, vectors).
/// Issue #3694: Extended KPIs for Enterprise Admin Dashboard.
/// </summary>
internal interface IResourceMetricsService
{
    /// <summary>
    /// Get current token balance and limit from OpenRouter.
    /// </summary>
    Task<(decimal CurrentEur, decimal LimitEur)> GetTokenBalanceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get database storage metrics (current size, limit, growth rate).
    /// </summary>
    Task<(decimal CurrentGb, decimal LimitGb, decimal GrowthMbPerDay)> GetDatabaseMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cache hit rate metrics from Redis.
    /// </summary>
    Task<(double HitRatePercent, double TrendPercent)> GetCacheHitRateAsync(CancellationToken cancellationToken = default);
}

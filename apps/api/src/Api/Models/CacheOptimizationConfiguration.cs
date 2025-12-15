namespace Api.Models;

/// <summary>
/// Configuration for cache optimization features including metrics recording,
/// cache warming, and dynamic TTL strategies.
/// AI-10: Cache Optimization - Configuration for hot/warm/cold query classification.
/// </summary>
internal class CacheOptimizationConfiguration
{
    /// <summary>
    /// Enables cache metrics recording (hits/misses/evictions) via OpenTelemetry.
    /// Default: true (enabled).
    /// </summary>
    public bool MetricsEnabled { get; set; } = true;

    /// <summary>
    /// Enables proactive cache warming service for frequently accessed queries.
    /// Default: false (disabled for safety - requires explicit opt-in).
    /// </summary>
    public bool WarmingEnabled { get; set; }

    /// <summary>
    /// Number of top queries to pre-cache during warming cycle.
    /// Default: 50 queries per game.
    /// </summary>
    public int WarmingTopQueriesCount { get; set; } = 50;

    /// <summary>
    /// Startup delay in minutes before first cache warming cycle.
    /// Prevents warming from interfering with application startup.
    /// Default: 2 minutes. Supports fractional values (e.g., 0.01 = 600ms for testing).
    /// </summary>
    public double WarmingStartupDelayMinutes { get; set; } = 2;

    /// <summary>
    /// Interval in hours between periodic cache warming cycles.
    /// Default: 6 hours.
    /// </summary>
    public int WarmingIntervalHours { get; set; } = 6;

    /// <summary>
    /// Minimum hit count for a query to be classified as "hot".
    /// Hot queries get the longest TTL (HotQueryTtlHours).
    /// Default: 10 hits.
    /// </summary>
    public int HotQueryThreshold { get; set; } = 10;

    /// <summary>
    /// Minimum hit count for a query to be classified as "warm".
    /// Queries with hit count >= WarmQueryThreshold and < HotQueryThreshold are warm.
    /// Default: 3 hits.
    /// </summary>
    public int WarmQueryThreshold { get; set; } = 3;

    /// <summary>
    /// TTL in hours for hot queries (>= HotQueryThreshold hits).
    /// Default: 24 hours (1 day).
    /// </summary>
    public int HotQueryTtlHours { get; set; } = 24;

    /// <summary>
    /// TTL in hours for warm queries (>= WarmQueryThreshold, < HotQueryThreshold hits).
    /// Default: 6 hours.
    /// </summary>
    public int WarmQueryTtlHours { get; set; } = 6;

    /// <summary>
    /// TTL in hours for cold queries (< WarmQueryThreshold hits).
    /// Default: 1 hour.
    /// </summary>
    public int ColdQueryTtlHours { get; set; } = 1;

    /// <summary>
    /// Redis key prefix for frequency tracking sorted sets.
    /// Format: {prefix}{gameId} -> "meepleai:freq:game-guid"
    /// Default: "meepleai:freq:".
    /// </summary>
    public string FrequencyTrackerKeyPrefix { get; set; } = "meepleai:freq:";
}

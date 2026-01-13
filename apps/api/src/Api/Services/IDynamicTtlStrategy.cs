namespace Api.Services;

/// <summary>
/// Calculates dynamic Time-To-Live (TTL) for cache entries based on access frequency.
/// AI-10: Cache Optimization - Hot/warm/cold query classification for optimal cache retention.
/// </summary>
internal interface IDynamicTtlStrategy
{
    /// <summary>
    /// Calculates TTL based on query hit count classification.
    /// Classification tiers (configurable thresholds):
    /// - Hot: hitCount &gt;= HotQueryThreshold to HotQueryTtlHours (default: 24h)
    /// - Warm: hitCount &gt;= WarmQueryThreshold to WarmQueryTtlHours (default: 6h)
    /// - Cold: hitCount &lt; WarmQueryThreshold to ColdQueryTtlHours (default: 1h)
    /// </summary>
    /// <param name="hitCount">Number of cache hits for the query (must be &gt;= 0)</param>
    /// <returns>TTL duration for the cache entry</returns>
    /// <exception cref="ArgumentException">Thrown if hitCount is negative</exception>
    Task<TimeSpan> CalculateTtlAsync(int hitCount);
}

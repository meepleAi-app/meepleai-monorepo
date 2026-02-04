namespace Api.Services;

/// <summary>
/// Records cache operation metrics (hits, misses, evictions) to OpenTelemetry.
/// AI-10: Cache Optimization - Fire-and-forget metrics recording to avoid blocking cache operations.
/// </summary>
internal interface ICacheMetricsRecorder
{
    /// <summary>
    /// Records a cache hit metric with operation and cache type labels.
    /// Increments meepleai.cache.hits.total counter via OpenTelemetry.
    /// Fire-and-forget pattern ensures &lt;5ms latency impact on cache operations.
    /// </summary>
    /// <param name="operation">Cache operation type (e.g., "get", "set", "invalidate")</param>
    /// <param name="cacheType">Cache type (e.g., "ai_response", "vector_search")</param>
    Task RecordCacheHitAsync(string operation, string cacheType);

    /// <summary>
    /// Records a cache miss metric with operation and cache type labels.
    /// Increments meepleai.cache.misses.total counter via OpenTelemetry.
    /// </summary>
    /// <param name="operation">Cache operation type</param>
    /// <param name="cacheType">Cache type</param>
    Task RecordCacheMissAsync(string operation, string cacheType);

    /// <summary>
    /// Records a cache eviction metric with eviction reason.
    /// Increments meepleai.cache.evictions.total counter via OpenTelemetry.
    /// </summary>
    /// <param name="reason">Eviction reason (e.g., "ttl_expired", "memory_pressure", "manual")</param>
    Task RecordCacheEvictionAsync(string reason);

    /// <summary>
    /// Records a cache promotion event (L2 → L1).
    /// ISSUE-3494: Tracks cache tier promotions for hot data optimization.
    /// </summary>
    /// <param name="fromTier">Source tier (e.g., "l2_redis")</param>
    /// <param name="toTier">Destination tier (e.g., "l1_memory")</param>
    Task RecordCachePromotionAsync(string fromTier, string toTier);

    /// <summary>
    /// Records adaptive TTL calculation by frequency classification.
    /// ISSUE-3494: Tracks TTL distribution (high/medium/low frequency).
    /// </summary>
    /// <param name="classification">Frequency classification (high/medium/low)</param>
    /// <param name="ttlSeconds">Calculated TTL in seconds</param>
    Task RecordAdaptiveTtlAsync(string classification, double ttlSeconds);

    /// <summary>
    /// Records cache operation latency for performance monitoring.
    /// ISSUE-3494: Tracks get/set operation latency for P95 analysis.
    /// </summary>
    /// <param name="operation">Operation type (get/set)</param>
    /// <param name="latencyMs">Latency in milliseconds</param>
    Task RecordCacheLatencyAsync(string operation, double latencyMs);
}

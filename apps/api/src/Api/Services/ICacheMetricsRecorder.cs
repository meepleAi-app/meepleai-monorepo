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
    /// Fire-and-forget pattern ensures <5ms latency impact on cache operations.
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
}

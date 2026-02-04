using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Caching;

/// <summary>
/// ISSUE-3494: Multi-tier cache interface for context retrieval optimization.
/// 3-tier strategy: In-Memory (μs) → Redis (ms) → Qdrant (100ms+).
/// Target: >80% cache hit rate for frequent rules and context elements.
/// </summary>
internal interface IMultiTierCache
{
    /// <summary>
    /// Gets a cached value from the fastest available tier with automatic promotion.
    /// Lookup order: L1 (memory) → L2 (Redis) → L3 (Qdrant).
    /// On cache hit from L2/L3, promotes value to faster tiers.
    /// </summary>
    /// <typeparam name="T">Type of cached value</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="gameId">Game ID for vector search context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached value or null if not found in any tier</returns>
    Task<MultiTierCacheResult<T>?> GetAsync<T>(
        string key,
        Guid gameId,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Gets or creates a cached value using the factory function.
    /// Provides cache stampede protection and automatic tier management.
    /// </summary>
    /// <typeparam name="T">Type of cached value</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="gameId">Game ID for context</param>
    /// <param name="factory">Factory function to create value if not cached</param>
    /// <param name="tags">Optional tags for cache invalidation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached or newly created value with tier info</returns>
    Task<MultiTierCacheResult<T>> GetOrCreateAsync<T>(
        string key,
        Guid gameId,
        Func<CancellationToken, Task<T>> factory,
        string[]? tags = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Sets a value in all appropriate cache tiers.
    /// Uses adaptive TTL based on access frequency.
    /// </summary>
    /// <typeparam name="T">Type of value to cache</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="gameId">Game ID for context</param>
    /// <param name="value">Value to cache</param>
    /// <param name="embedding">Optional embedding for L3 (Qdrant) storage</param>
    /// <param name="tags">Optional tags for cache invalidation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetAsync<T>(
        string key,
        Guid gameId,
        T value,
        Vector? embedding = null,
        string[]? tags = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Removes a value from all cache tiers.
    /// </summary>
    /// <param name="key">Cache key to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all cache entries matching the specified tag from all tiers.
    /// </summary>
    /// <param name="tag">Tag to match for removal</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entries removed</returns>
    Task<int> RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Warms the cache for a specific game by pre-loading frequently accessed context.
    /// Used during game session start to reduce cold start latency.
    /// </summary>
    /// <param name="gameId">Game ID to warm cache for</param>
    /// <param name="topN">Number of top frequent items to warm (default: 20)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of items warmed</returns>
    Task<int> WarmGameCacheAsync(
        Guid gameId,
        int topN = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current cache metrics and statistics.
    /// </summary>
    /// <returns>Multi-tier cache metrics</returns>
    MultiTierCacheMetrics GetMetrics();

    /// <summary>
    /// Calculates adaptive TTL based on access frequency.
    /// High frequency (100+ hits): 24h TTL.
    /// Medium frequency (10+ hits): 1h TTL.
    /// Low frequency: 5min TTL.
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="key">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Calculated TTL based on access pattern</returns>
    Task<TimeSpan> CalculateAdaptiveTtlAsync(
        Guid gameId,
        string key,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from multi-tier cache lookup with tier information.
/// </summary>
/// <typeparam name="T">Type of cached value</typeparam>
internal class MultiTierCacheResult<T> where T : class
{
    /// <summary>
    /// The cached value.
    /// </summary>
    public required T Value { get; init; }

    /// <summary>
    /// Which tier the value was retrieved from.
    /// </summary>
    public required CacheTier SourceTier { get; init; }

    /// <summary>
    /// Time taken to retrieve the value in milliseconds.
    /// </summary>
    public required double RetrievalTimeMs { get; init; }

    /// <summary>
    /// Whether the value was promoted to faster tiers.
    /// </summary>
    public bool WasPromoted { get; init; }
}

/// <summary>
/// Cache tier identification for metrics and debugging.
/// </summary>
internal enum CacheTier
{
    /// <summary>
    /// L1: In-memory LRU cache (microseconds latency).
    /// </summary>
    L1Memory = 1,

    /// <summary>
    /// L2: Redis distributed cache (milliseconds latency).
    /// </summary>
    L2Redis = 2,

    /// <summary>
    /// L3: Qdrant vector database (100ms+ latency).
    /// </summary>
    L3Qdrant = 3,

    /// <summary>
    /// Value was created by factory (cache miss on all tiers).
    /// </summary>
    Factory = 4
}

/// <summary>
/// Multi-tier cache metrics for monitoring and optimization.
/// </summary>
internal class MultiTierCacheMetrics
{
    /// <summary>
    /// L1 (in-memory) cache statistics.
    /// </summary>
    public required TierMetrics L1Memory { get; init; }

    /// <summary>
    /// L2 (Redis) cache statistics.
    /// </summary>
    public required TierMetrics L2Redis { get; init; }

    /// <summary>
    /// L3 (Qdrant) cache statistics.
    /// </summary>
    public required TierMetrics L3Qdrant { get; init; }

    /// <summary>
    /// Overall hit rate across all tiers (0-100).
    /// Target: >80%.
    /// </summary>
    public double OverallHitRatePercent
    {
        get
        {
            var totalHits = L1Memory.Hits + L2Redis.Hits + L3Qdrant.Hits;
            var totalMisses = L1Memory.Misses; // Only count final misses
            var total = totalHits + totalMisses;
            return total > 0 ? (totalHits * 100.0) / total : 0;
        }
    }

    /// <summary>
    /// Number of cache promotions (L3→L2, L2→L1).
    /// </summary>
    public long TotalPromotions { get; init; }

    /// <summary>
    /// Number of adaptive TTL adjustments.
    /// </summary>
    public long AdaptiveTtlAdjustments { get; init; }

    /// <summary>
    /// Average retrieval latency in milliseconds.
    /// </summary>
    public double AverageLatencyMs { get; init; }
}

/// <summary>
/// Statistics for a single cache tier.
/// </summary>
internal class TierMetrics
{
    /// <summary>
    /// Number of cache hits.
    /// </summary>
    public long Hits { get; set; }

    /// <summary>
    /// Number of cache misses.
    /// </summary>
    public long Misses { get; set; }

    /// <summary>
    /// Current number of entries.
    /// </summary>
    public long EntryCount { get; set; }

    /// <summary>
    /// Hit rate percentage (0-100).
    /// </summary>
    public double HitRatePercent
    {
        get
        {
            var total = Hits + Misses;
            return total > 0 ? (Hits * 100.0) / total : 0;
        }
    }

    /// <summary>
    /// Average latency for this tier in milliseconds.
    /// </summary>
    public double AverageLatencyMs { get; set; }
}

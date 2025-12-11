

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services;

/// <summary>
/// PERF-05: HybridCache service for AI/RAG responses with L1 (in-memory) + L2 (Redis) support.
/// Provides cache stampede elimination via GetOrCreateAsync pattern.
/// </summary>
/// <remarks>
/// Key benefits over manual Redis caching (IAiResponseCacheService):
/// - **Cache Stampede Protection**: Multiple concurrent requests for same uncached key execute factory only once
/// - **L1 + L2 Tiering**: Fast in-memory L1 cache + distributed Redis L2 for multi-server scenarios
/// - **Automatic Serialization**: Built-in JSON serialization for complex types
/// - **Tag-Based Invalidation**: Batch invalidation by tags (game, PDF, endpoint)
/// - **Timeout Protection**: L2 failures don't block requests (falls back to L1)
///
/// Usage pattern:
/// <code>
/// var response = await hybridCache.GetOrCreateAsync(
///     cacheKey: "qa:chess:how-does-knight-move",
///     factory: async ct => await GenerateAiResponse(...),
///     tags: ["game:chess", "endpoint:qa"],
///     expiration: TimeSpan.FromHours(24),
///     ct
/// );
/// </code>
/// </remarks>
public interface IHybridCacheService
{
    /// <summary>
    /// Get cached value or create it using the factory function.
    /// Provides cache stampede protection: only one factory executes per key.
    /// </summary>
    /// <typeparam name="T">Type of cached value (must be JSON-serializable)</typeparam>
    /// <param name="cacheKey">Unique cache key</param>
    /// <param name="factory">Async factory function to generate value if not cached</param>
    /// <param name="tags">Optional cache tags for batch invalidation (e.g., "game:chess", "endpoint:qa")</param>
    /// <param name="expiration">Optional cache expiration (null = use default from config)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Cached or newly generated value</returns>
    /// <remarks>
    /// Cache stampede protection:
    /// - If 100 concurrent requests arrive for same uncached key
    /// - Only ONE factory function executes
    /// - Other 99 requests wait and receive the same result
    /// - This eliminates redundant AI API calls (OpenRouter, embeddings, etc.)
    /// </remarks>
    Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        string[]? tags = null,
        TimeSpan? expiration = null,
        CancellationToken ct = default) where T : class;

    /// <summary>
    /// Remove a specific cache entry by key.
    /// </summary>
    /// <param name="cacheKey">Cache key to remove</param>
    /// <param name="ct">Cancellation token</param>
    Task RemoveAsync(string cacheKey, CancellationToken ct = default);

    /// <summary>
    /// Remove all cache entries with the specified tag.
    /// </summary>
    /// <param name="tag">Tag to filter cache entries (e.g., "game:chess", "pdf:abc123")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of cache entries removed</returns>
    /// <remarks>
    /// Common tag patterns:
    /// - "game:{gameId}" → Invalidate all game-related cache entries
    /// - "pdf:{pdfId}" → Invalidate all PDF-related cache entries
    /// - "endpoint:qa" → Invalidate all QA endpoint cache entries
    /// - "endpoint:explain" → Invalidate all Explain endpoint cache entries
    /// - "endpoint:setup" → Invalidate all Setup Guide cache entries
    /// </remarks>
    Task<int> RemoveByTagAsync(string tag, CancellationToken ct = default);

    /// <summary>
    /// Remove all cache entries matching multiple tags (AND logic).
    /// </summary>
    /// <param name="tags">Tags that must ALL be present on cache entries</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of cache entries removed</returns>
    /// <remarks>
    /// Example: RemoveByTagsAsync(["game:chess", "endpoint:qa"])
    /// → Removes only QA cache entries for Chess game
    /// </remarks>
    Task<int> RemoveByTagsAsync(string[] tags, CancellationToken ct = default);

    /// <summary>
    /// Get cache statistics for monitoring and debugging.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Cache statistics (hit rate, entry count, memory usage)</returns>
    Task<HybridCacheStats> GetStatsAsync(CancellationToken ct = default);
}

/// <summary>
/// PERF-05: Statistics for HybridCache monitoring.
/// </summary>
public class HybridCacheStats
{
    /// <summary>
    /// Total number of cache hits since startup.
    /// </summary>
    public long TotalHits { get; set; }

    /// <summary>
    /// Total number of cache misses since startup.
    /// </summary>
    public long TotalMisses { get; set; }

    /// <summary>
    /// Cache hit rate percentage (0-100).
    /// </summary>
    public double HitRatePercentage => TotalHits + TotalMisses > 0
        ? (TotalHits * 100.0) / (TotalHits + TotalMisses)
        : 0;

    /// <summary>
    /// Current number of entries in L1 (in-memory) cache.
    /// </summary>
    public long L1EntryCount { get; set; }

    /// <summary>
    /// Estimated memory usage of L1 cache in bytes.
    /// </summary>
    public long L1MemoryBytes { get; set; }

    /// <summary>
    /// Whether L2 (Redis) cache is enabled.
    /// </summary>
    public bool L2Enabled { get; set; }

    /// <summary>
    /// Number of cache stampede protection events (factory executions prevented).
    /// </summary>
    /// <remarks>
    /// High value indicates HybridCache is successfully preventing redundant work.
    /// Example: 100 concurrent requests → 1 factory execution → 99 stampede protections
    /// </remarks>
    public long StampedePreventions { get; set; }
}

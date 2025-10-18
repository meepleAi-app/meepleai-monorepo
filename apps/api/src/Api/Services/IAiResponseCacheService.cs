namespace Api.Services;

/// <summary>
/// AI-05: Cache service for AI-generated responses (QA, Explain, Setup)
/// Provides Redis-backed caching to reduce latency for frequently asked questions.
/// </summary>
public interface IAiResponseCacheService
{
    /// <summary>
    /// Get cached response for a given cache key.
    /// </summary>
    /// <typeparam name="T">Type of response (QaResponse, ExplainResponse, SetupGuideResponse)</typeparam>
    /// <param name="cacheKey">Cache key</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Cached response if found, null otherwise</returns>
    Task<T?> GetAsync<T>(string cacheKey, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Set cached response with TTL.
    /// </summary>
    /// <typeparam name="T">Type of response (QaResponse, ExplainResponse, SetupGuideResponse)</typeparam>
    /// <param name="cacheKey">Cache key</param>
    /// <param name="response">Response to cache</param>
    /// <param name="ttlSeconds">Time-to-live in seconds (default: 24 hours)</param>
    /// <param name="ct">Cancellation token</param>
    Task SetAsync<T>(string cacheKey, T response, int ttlSeconds = 86400, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Generate cache key for QA endpoint.
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="query">User query</param>
    /// <returns>Cache key</returns>
    string GenerateQaCacheKey(string gameId, string query);

    /// <summary>
    /// Generate cache key for Explain endpoint.
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="topic">Topic to explain</param>
    /// <returns>Cache key</returns>
    string GenerateExplainCacheKey(string gameId, string topic);

    /// <summary>
    /// Generate cache key for Setup endpoint.
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <returns>Cache key</returns>
    string GenerateSetupCacheKey(string gameId);

    /// <summary>
    /// Invalidate all cached responses for a specific game across every AI endpoint.
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="ct">Cancellation token</param>
    Task InvalidateGameAsync(string gameId, CancellationToken ct = default);

    /// <summary>
    /// Invalidate cached responses for a specific AI endpoint (QA, Explain, Setup) for a game.
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="endpoint">Target endpoint namespace</param>
    /// <param name="ct">Cancellation token</param>
    Task InvalidateEndpointAsync(string gameId, AiCacheEndpoint endpoint, CancellationToken ct = default);

    /// <summary>
    /// PERF-03: Invalidate all cache entries associated with a specific tag.
    /// </summary>
    /// <param name="tag">Cache tag (e.g., "game:chess", "pdf:abc123")</param>
    /// <param name="ct">Cancellation token</param>
    Task InvalidateByCacheTagAsync(string tag, CancellationToken ct = default);

    /// <summary>
    /// PERF-03: Get cache statistics with optional game filter.
    /// </summary>
    /// <param name="gameId">Optional game ID filter</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Cache statistics including hit rate, top questions, and cache size</returns>
    Task<CacheStats> GetCacheStatsAsync(string? gameId = null, CancellationToken ct = default);

    /// <summary>
    /// PERF-03: Record cache access (hit or miss) for tracking statistics.
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="questionHash">Hash of the question</param>
    /// <param name="isHit">True if cache hit, false if cache miss</param>
    /// <param name="ct">Cancellation token</param>
    Task RecordCacheAccessAsync(string gameId, string questionHash, bool isHit, CancellationToken ct = default);
}

/// <summary>
/// Namespaces for AI cache entries.
/// </summary>
public enum AiCacheEndpoint
{
    Qa,
    Explain,
    Setup
}

/// <summary>
/// PERF-03: Cache statistics response.
/// </summary>
public class CacheStats
{
    /// <summary>
    /// Total cache hits across all questions.
    /// </summary>
    public long TotalHits { get; set; }

    /// <summary>
    /// Total cache misses across all questions.
    /// </summary>
    public long TotalMisses { get; set; }

    /// <summary>
    /// Cache hit rate (hits / total requests). Range: 0.0 to 1.0.
    /// </summary>
    public double HitRate { get; set; }

    /// <summary>
    /// Total number of cached keys in Redis.
    /// </summary>
    public int TotalKeys { get; set; }

    /// <summary>
    /// Total cache size in bytes (approximate).
    /// </summary>
    public long CacheSizeBytes { get; set; }

    /// <summary>
    /// Top 10 cached questions ordered by hit count descending.
    /// </summary>
    public List<TopQuestion> TopQuestions { get; set; } = new();
}

/// <summary>
/// PERF-03: Top cached question entry.
/// </summary>
public class TopQuestion
{
    /// <summary>
    /// Hash of the question.
    /// </summary>
    public string QuestionHash { get; set; } = string.Empty;

    /// <summary>
    /// Number of times this question was a cache hit.
    /// </summary>
    public long HitCount { get; set; }

    /// <summary>
    /// Number of times this question was a cache miss.
    /// </summary>
    public long MissCount { get; set; }

    /// <summary>
    /// When this cache entry was last accessed.
    /// </summary>
    public DateTime LastHitAt { get; set; }
}

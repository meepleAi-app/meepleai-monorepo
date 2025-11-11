namespace Api.Configuration;

/// <summary>
/// PERF-05: Configuration for HybridCache L1 (in-memory) and L2 (distributed) caching.
/// </summary>
public class HybridCacheConfiguration
{
    /// <summary>
    /// Enable L2 distributed cache layer (Redis). If false, only L1 in-memory cache is used.
    /// </summary>
    /// <remarks>
    /// Development: false (in-memory only for simplicity)
    /// Staging/Production: true (L1 + Redis L2 for multi-server)
    /// </remarks>
    public bool EnableL2Cache { get; set; } = false;

    /// <summary>
    /// Maximum size of individual cached entries in bytes.
    /// Default: 10MB (10485760 bytes)
    /// </summary>
    /// <remarks>
    /// AI responses typically 1-5KB, embeddings 3-4KB, PDF text 10-100KB.
    /// 10MB allows for large PDF text chunks and batch embeddings.
    /// </remarks>
    public long MaximumPayloadBytes { get; set; } = 10_485_760; // 10 MB

    /// <summary>
    /// Default expiration time for cache entries if not explicitly specified.
    /// Default: 24 hours (86400 seconds)
    /// </summary>
    /// <remarks>
    /// AI responses: 24 hours (rule specs rarely change)
    /// Embeddings: 7 days (stable unless PDF re-uploaded)
    /// RAG results: 1 hour (balance freshness vs performance)
    /// </remarks>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Maximum time to wait for L2 cache operations before falling back to L1.
    /// Default: 2 seconds
    /// </summary>
    /// <remarks>
    /// Prevents L2 Redis latency from blocking requests.
    /// If Redis is slow/unavailable, HybridCache falls back to L1 memory cache.
    /// </remarks>
    public TimeSpan L2Timeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum number of concurrent factory calls for the same cache key (cache stampede protection).
    /// Default: 1 (only one factory per key executes, others wait)
    /// </summary>
    /// <remarks>
    /// HybridCache built-in stampede protection:
    /// - When multiple requests arrive for same uncached key
    /// - Only ONE factory method executes (generates AI response, embeddings, etc.)
    /// - All other requests wait and receive the same result
    /// - This is the primary benefit over manual Redis caching
    /// </remarks>
    public int MaxConcurrentFactories { get; set; } = 1;

    /// <summary>
    /// Enable cache tag support for invalidation.
    /// Default: true
    /// </summary>
    /// <remarks>
    /// Tags allow batch invalidation:
    /// - "game:{gameId}" → Invalidate all game-related cache entries
    /// - "pdf:{pdfId}" → Invalidate all PDF-related cache entries
    /// - "endpoint:qa" → Invalidate all QA endpoint cache entries
    /// </remarks>
    public bool EnableTags { get; set; } = true;

    /// <summary>
    /// Maximum number of cache tags per entry.
    /// Default: 10
    /// </summary>
    public int MaxTagsPerEntry { get; set; } = 10;
}

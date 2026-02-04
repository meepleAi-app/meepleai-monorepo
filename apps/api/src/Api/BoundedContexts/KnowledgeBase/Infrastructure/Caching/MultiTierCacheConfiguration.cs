namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Caching;

/// <summary>
/// ISSUE-3494: Configuration for multi-tier cache system.
/// 3-tier strategy with adaptive TTL based on access frequency.
/// </summary>
internal class MultiTierCacheConfiguration
{
    /// <summary>
    /// Configuration section name in appsettings.
    /// </summary>
    public const string SectionName = "MultiTierCache";

    /// <summary>
    /// Enable multi-tier caching. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    // === L1: In-Memory LRU Cache ===

    /// <summary>
    /// Maximum number of items in L1 in-memory cache.
    /// Default: 1000 items (as per issue requirements).
    /// </summary>
    public int L1MaxItems { get; set; } = 1000;

    /// <summary>
    /// Default TTL for L1 cache entries.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan L1DefaultTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Enable L1 cache tier.
    /// </summary>
    public bool L1Enabled { get; set; } = true;

    // === L2: Redis Distributed Cache ===

    /// <summary>
    /// Enable L2 Redis cache tier.
    /// </summary>
    public bool L2Enabled { get; set; } = true;

    /// <summary>
    /// Default TTL for L2 Redis cache entries.
    /// Default: 1 hour.
    /// </summary>
    public TimeSpan L2DefaultTtl { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Timeout for L2 Redis operations.
    /// Default: 2 seconds.
    /// </summary>
    public TimeSpan L2Timeout { get; set; } = TimeSpan.FromSeconds(2);

    // === L3: Qdrant Vector Store ===

    /// <summary>
    /// Enable L3 Qdrant cache tier.
    /// </summary>
    public bool L3Enabled { get; set; } = true;

    /// <summary>
    /// Timeout for L3 Qdrant operations.
    /// Default: 5 seconds.
    /// </summary>
    public TimeSpan L3Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Minimum similarity score for L3 vector search results.
    /// Default: 0.85 (high similarity).
    /// </summary>
    public double L3MinSimilarityScore { get; set; } = 0.85;

    // === Adaptive TTL Configuration ===

    /// <summary>
    /// Access count threshold for high-frequency classification.
    /// Entries with ≥HighFrequencyThreshold accesses get longest TTL.
    /// Default: 100 hits (as per issue requirements).
    /// </summary>
    public int HighFrequencyThreshold { get; set; } = 100;

    /// <summary>
    /// Access count threshold for medium-frequency classification.
    /// Entries with accesses between MediumFrequencyThreshold and HighFrequencyThreshold get medium TTL.
    /// Default: 10 hits (as per issue requirements).
    /// </summary>
    public int MediumFrequencyThreshold { get; set; } = 10;

    /// <summary>
    /// TTL for high-frequency items (100+ accesses).
    /// Default: 24 hours (as per issue requirements).
    /// </summary>
    public TimeSpan HighFrequencyTtl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// TTL for medium-frequency items (10+ accesses, below HighFrequencyThreshold).
    /// Default: 1 hour (as per issue requirements).
    /// </summary>
    public TimeSpan MediumFrequencyTtl { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// TTL for low-frequency items (below MediumFrequencyThreshold).
    /// Default: 5 minutes (as per issue requirements).
    /// </summary>
    public TimeSpan LowFrequencyTtl { get; set; } = TimeSpan.FromMinutes(5);

    // === Cache Promotion ===

    /// <summary>
    /// Enable automatic cache promotion from slower to faster tiers.
    /// When enabled, L3 hits are promoted to L2, and L2 hits are promoted to L1.
    /// Default: true.
    /// </summary>
    public bool EnablePromotion { get; set; } = true;

    /// <summary>
    /// Minimum access count before promoting from L3 to L2.
    /// Prevents one-time queries from filling Redis.
    /// Default: 2 accesses.
    /// </summary>
    public int PromotionToL2Threshold { get; set; } = 2;

    /// <summary>
    /// Minimum access count before promoting from L2 to L1.
    /// Prevents rarely accessed items from consuming memory.
    /// Default: 3 accesses.
    /// </summary>
    public int PromotionToL1Threshold { get; set; } = 3;

    // === Cache Warming ===

    /// <summary>
    /// Enable cache warming on game session start.
    /// </summary>
    public bool WarmingEnabled { get; set; } = true;

    /// <summary>
    /// Number of top frequent items to warm per game.
    /// Default: 20 (as per issue requirements).
    /// </summary>
    public int WarmingTopNItems { get; set; } = 20;

    /// <summary>
    /// Maximum time to spend warming cache on game start.
    /// Default: 5 seconds.
    /// </summary>
    public TimeSpan WarmingTimeout { get; set; } = TimeSpan.FromSeconds(5);

    // === Metrics ===

    /// <summary>
    /// Enable metrics recording for cache operations.
    /// Default: true.
    /// </summary>
    public bool MetricsEnabled { get; set; } = true;

    /// <summary>
    /// Key prefix for multi-tier cache entries in Redis.
    /// Default: "mtc:".
    /// </summary>
    public string KeyPrefix { get; set; } = "mtc:";

    /// <summary>
    /// Target cache hit rate percentage.
    /// Used for alerting when hit rate drops below target.
    /// Default: 80%.
    /// </summary>
    public double TargetHitRatePercent { get; set; } = 80.0;
}

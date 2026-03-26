// OPS-02: Cache metrics (hits, misses, evictions, TTL, L1 gauge, dashboard invalidations)
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// Counter for cache hits
    /// </summary>
    public static readonly Counter<long> CacheHitsTotal = Meter.CreateCounter<long>(
        name: "meepleai.cache.hits.total",
        unit: "hits",
        description: "Total number of cache hits");

    /// <summary>
    /// Counter for cache misses
    /// </summary>
    public static readonly Counter<long> CacheMissesTotal = Meter.CreateCounter<long>(
        name: "meepleai.cache.misses.total",
        unit: "misses",
        description: "Total number of cache misses");

    /// <summary>
    /// Counter for cache evictions
    /// </summary>
    public static readonly Counter<long> CacheEvictionsTotal = Meter.CreateCounter<long>(
        name: "meepleai.cache.evictions.total",
        unit: "evictions",
        description: "Total number of cache evictions");

    /// <summary>
    /// Counter for cache promotions (L2 → L1).
    /// ISSUE-3494: Tracks when items are promoted to faster cache tiers.
    /// </summary>
    public static readonly Counter<long> CachePromotionsTotal = Meter.CreateCounter<long>(
        name: "meepleai.cache.promotions.total",
        unit: "promotions",
        description: "Total number of cache promotions from L2 to L1");

    /// <summary>
    /// Counter for adaptive TTL adjustments by frequency classification.
    /// ISSUE-3494: Tracks TTL distribution (high/medium/low frequency).
    /// </summary>
    public static readonly Counter<long> CacheTtlAdjustmentsTotal = Meter.CreateCounter<long>(
        name: "meepleai.cache.ttl_adjustments.total",
        unit: "adjustments",
        description: "Adaptive TTL adjustments by frequency classification");

    /// <summary>
    /// Histogram for cache operation latency in milliseconds.
    /// ISSUE-3494: Tracks get/set operation latency for P95 monitoring.
    /// </summary>
    public static readonly Histogram<double> CacheOperationLatency = Meter.CreateHistogram<double>(
        name: "meepleai.cache.operation.latency",
        unit: "ms",
        description: "Cache operation latency in milliseconds");

    /// <summary>
    /// Gauge for L1 cache entry count.
    /// ISSUE-3494: Tracks in-memory cache size (max: 1000).
    /// </summary>
    public static readonly ObservableGauge<int> CacheL1EntryCount = Meter.CreateObservableGauge<int>(
        name: "meepleai.cache.l1.entry_count",
        observeValue: () => 0, // Will be updated by MultiTierCache
        unit: "entries",
        description: "Current number of entries in L1 in-memory cache");

    /// <summary>
    /// Counter for dashboard cache invalidations triggered by configuration changes.
    /// Issue #879: Tracks cache invalidation events for monitoring and observability.
    /// </summary>
    public static readonly Counter<long> DashboardCacheInvalidationsTotal = Meter.CreateCounter<long>(
        name: "meepleai.cache.dashboard.invalidations.total",
        unit: "invalidations",
        description: "Dashboard cache invalidations triggered by configuration changes");

    /// <summary>
    /// Records cache hit or miss
    /// </summary>
    public static void RecordCacheAccess(bool isHit, string? cacheType = null)
    {
        var tags = new TagList();
        if (!string.IsNullOrEmpty(cacheType))
        {
            tags.Add("cache.type", cacheType);
        }

        if (isHit)
        {
            CacheHitsTotal.Add(1, tags);
        }
        else
        {
            CacheMissesTotal.Add(1, tags);
        }
    }
}

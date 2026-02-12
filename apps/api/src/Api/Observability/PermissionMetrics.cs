using System.Diagnostics.Metrics;

namespace Api.Observability;

/// <summary>
/// Prometheus metrics for permission system (Epic #4068 - Issue #4177)
/// </summary>
public static class PermissionMetrics
{
    private static readonly Meter Meter = new("MeepleAI.Permissions", "1.0.0");

    // Counters
    public static readonly Counter<long> CheckTotal = Meter.CreateCounter<long>(
        "permission_check_total",
        "count",
        "Total permission checks performed");

    public static readonly Counter<long> DeniedTotal = Meter.CreateCounter<long>(
        "permission_denied_total",
        "count",
        "Total permission checks denied");

    public static readonly Counter<long> CacheHitTotal = Meter.CreateCounter<long>(
        "permission_cache_hit_total",
        "count",
        "Permission cache hits");

    public static readonly Counter<long> CacheMissTotal = Meter.CreateCounter<long>(
        "permission_cache_miss_total",
        "count",
        "Permission cache misses");

    // Histogram for latency
    public static readonly Histogram<double> CheckDuration = Meter.CreateHistogram<double>(
        "permission_check_duration_ms",
        "milliseconds",
        "Duration of permission checks");

    public static readonly Histogram<double> CacheAccessDuration = Meter.CreateHistogram<double>(
        "permission_cache_access_duration_ms",
        "milliseconds",
        "Duration of cache access operations");

    // Gauges
    private static long _activePermissionChecks = 0;

    public static readonly ObservableGauge<long> ActiveChecks = Meter.CreateObservableGauge(
        "permission_active_checks",
        () => _activePermissionChecks,
        "count",
        "Active permission checks in progress");

    // Helper methods
    public static void IncrementActiveChecks() => Interlocked.Increment(ref _activePermissionChecks);
    public static void DecrementActiveChecks() => Interlocked.Decrement(ref _activePermissionChecks);

    // Labeled metrics (per feature)
    public static void RecordCheck(string feature, bool granted, double durationMs)
    {
        var tags = new TagList
        {
            { "feature", feature },
            { "result", granted ? "granted" : "denied" }
        };

        CheckTotal.Add(1, tags);

        if (!granted)
        {
            DeniedTotal.Add(1, tags);
        }

        CheckDuration.Record(durationMs, tags);
    }

    // Cache metrics
    public static void RecordCacheAccess(bool hit, double durationMs)
    {
        var tags = new TagList { { "result", hit ? "hit" : "miss" } };

        if (hit)
        {
            CacheHitTotal.Add(1, tags);
        }
        else
        {
            CacheMissTotal.Add(1, tags);
        }

        CacheAccessDuration.Record(durationMs, tags);
    }
}

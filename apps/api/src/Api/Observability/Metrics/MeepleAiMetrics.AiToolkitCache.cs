// ADR-069 follow-up (#2383): AiToolkit suggestion cache observability.
//
// Cardinality policy (matches MeepleAiMetrics.SharedGameDetail pattern, #614):
// counters are aggregate (no per-game-id label) to keep the metric cardinality
// bounded. Per-game breakdown is available via the structured-log entries fired
// by the handler/event-handler at the same call sites (game_id is logged via
// LogInformation, not via Prometheus label).
//
// PR #2409 open question #1 resolution (#2383 brainstorm 2026-06-16).
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    private static readonly Counter<long> AiToolkitCacheHits = Meter.CreateCounter<long>(
        name: "meepleai_aitoolkit_cache_hit_total",
        description: "AiToolkit suggestion cache hits — ADR-069 #2383");

    private static readonly Counter<long> AiToolkitCacheMisses = Meter.CreateCounter<long>(
        name: "meepleai_aitoolkit_cache_miss_total",
        description: "AiToolkit suggestion cache misses leading to LLM call — ADR-069 #2383");

    private static readonly Counter<long> AiToolkitCacheInvalidations = Meter.CreateCounter<long>(
        name: "meepleai_aitoolkit_cache_invalidated_total",
        description: "AiToolkit suggestion cache entries invalidated by KbDocIndexedEvent — ADR-069 #2383");

    /// <summary>Records a cache hit for the AiToolkit suggestion.</summary>
    /// <remarks>
    /// Aggregate counter (no per-game label per #614 cardinality policy).
    /// Per-game breakdown is available via structured logs at the call site.
    /// </remarks>
    public static void RecordAiToolkitCacheHit() =>
        AiToolkitCacheHits.Add(1);

    /// <summary>Records a cache miss (LLM pipeline will run).</summary>
    /// <remarks>Aggregate counter; see <see cref="RecordAiToolkitCacheHit"/> for cardinality rationale.</remarks>
    public static void RecordAiToolkitCacheMiss() =>
        AiToolkitCacheMisses.Add(1);

    /// <summary>Records a cache invalidation triggered by <c>KbDocIndexedEvent</c>.</summary>
    /// <remarks>Aggregate counter; see <see cref="RecordAiToolkitCacheHit"/> for cardinality rationale.</remarks>
    public static void RecordAiToolkitCacheInvalidated() =>
        AiToolkitCacheInvalidations.Add(1);
}

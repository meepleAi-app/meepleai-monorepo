// ADR-069 follow-up (#2383): AiToolkit suggestion cache observability
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

    /// <summary>Records a cache hit for the AiToolkit suggestion of the given game.</summary>
    public static void RecordAiToolkitCacheHit(Guid gameId) =>
        AiToolkitCacheHits.Add(1, new KeyValuePair<string, object?>("game_id", gameId));

    /// <summary>Records a cache miss (LLM pipeline will run) for the given game.</summary>
    public static void RecordAiToolkitCacheMiss(Guid gameId) =>
        AiToolkitCacheMisses.Add(1, new KeyValuePair<string, object?>("game_id", gameId));

    /// <summary>Records a cache invalidation triggered by <c>KbDocIndexedEvent</c> for the given game.</summary>
    public static void RecordAiToolkitCacheInvalidated(Guid gameId) =>
        AiToolkitCacheInvalidations.Add(1, new KeyValuePair<string, object?>("game_id", gameId));
}

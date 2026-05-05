// Issue #614 (Wave A.4 follow-up · P0): SharedGame detail-page observability.
//
// Spec §3.7 defined four metrics for the new `/shared-games/{id}` endpoint
// (PR #605 implemented zero of them). Without instrumentation we cannot:
//   • detect cache stampede (cache_hits stuck at 0%)
//   • alert on slow cross-BC fan-out (toolkit/agent/kb)
//   • measure end-to-end render p95 against the 200ms SLO
//   • correlate frontend error.tsx rate with backend issues
//
// Cardinality budget — every label is sourced from a closed enum exposed via
// the nested `*Labels` static classes below. Total series count for this
// partial is bounded at:
//   RequestsTotal              = 3 (success|not_found|error)
//   CacheHitsTotal             = 2 (hit|origin) — see CacheHitSources comment
//   RenderDurationSeconds      = 2 (hit|origin)
//   CrossBcQueryDurationSeconds= 3 (toolkit|agent|kb)
// Total: 10 active series. Far below the agreed 100-per-context budget.
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// Counter for shared-game detail requests, broken down by terminal result.
    /// Issue #614 (spec §3.7).
    /// </summary>
    public static readonly Counter<long> SharedGameDetailRequestsTotal = Meter.CreateCounter<long>(
        name: "meepleai.shared_game_detail.requests.total",
        unit: "requests",
        description: "Shared-game detail-page requests by terminal result (success/not_found/error)");

    /// <summary>
    /// Counter for shared-game detail cache hits, broken down by source tier.
    /// Issue #614 (spec §3.7).
    /// <para>
    /// HybridCache (.NET 9) does not expose whether a hit was served from the
    /// L1 (in-process) or L2 (Redis) tier — only whether the factory delegate
    /// ran. We therefore collapse both tiers into <see cref="CacheHitSources.Hit"/>
    /// and reserve <see cref="CacheHitSources.Origin"/> for full misses. The
    /// label key remains <c>source</c> so a future PR can split the bucket
    /// without renaming the metric.
    /// </para>
    /// </summary>
    public static readonly Counter<long> SharedGameDetailCacheHitsTotal = Meter.CreateCounter<long>(
        name: "meepleai.shared_game_detail.cache_hits.total",
        unit: "events",
        description: "Shared-game detail cache lookups by source (hit=hybrid/redis, origin=DB)");

    /// <summary>
    /// Histogram for end-to-end render duration (seconds) of the shared-game
    /// detail handler. Includes cache lookup + factory + DTO assembly.
    /// Issue #614 (spec §3.7).
    /// </summary>
    public static readonly Histogram<double> SharedGameDetailRenderDurationSeconds = Meter.CreateHistogram<double>(
        name: "meepleai.shared_game_detail.render_duration_seconds",
        unit: "s",
        description: "End-to-end render duration of the shared-game detail handler in seconds");

    /// <summary>
    /// Histogram for cross-bounded-context fan-out query duration (seconds).
    /// Issue #614 (spec §3.7). Toolkit / Agent / KB previews each emit one
    /// sample so we can attribute slow detail renders to the right BC.
    /// </summary>
    public static readonly Histogram<double> SharedGameDetailCrossBcQueryDurationSeconds = Meter.CreateHistogram<double>(
        name: "meepleai.shared_game_detail.cross_bc_query_duration_seconds",
        unit: "s",
        description: "Cross-BC query duration per fan-out (toolkit/agent/kb) in seconds");

    /// <summary>
    /// Stable identifiers for the <c>result</c> label on
    /// <see cref="SharedGameDetailRequestsTotal"/>.
    /// </summary>
    public static class SharedGameDetailResults
    {
        public const string Success = "success";
        public const string NotFound = "not_found";
        public const string Error = "error";
    }

    /// <summary>
    /// Stable identifiers for the <c>source</c> label on
    /// <see cref="SharedGameDetailCacheHitsTotal"/>.
    /// See class XML doc for why <c>hybrid</c>/<c>redis</c> collapse to <c>hit</c>.
    /// </summary>
    public static class CacheHitSources
    {
        public const string Hit = "hit";
        public const string Origin = "origin";
    }

    /// <summary>
    /// Stable identifiers for the <c>cache_outcome</c> label on
    /// <see cref="SharedGameDetailRenderDurationSeconds"/>. Mirrors
    /// <see cref="CacheHitSources"/> so render-duration histograms can be
    /// joined to cache-hit counters in PromQL.
    /// </summary>
    public static class SharedGameDetailCacheOutcomes
    {
        public const string Hit = "hit";
        public const string Origin = "origin";
    }

    /// <summary>
    /// Stable identifiers for the <c>bounded_context</c> label on
    /// <see cref="SharedGameDetailCrossBcQueryDurationSeconds"/>.
    /// </summary>
    public static class SharedGameDetailBoundedContexts
    {
        public const string Toolkit = "toolkit";
        public const string Agent = "agent";
        public const string Kb = "kb";
    }

    /// <summary>
    /// Records a terminal request outcome.
    /// </summary>
    /// <param name="result">One of <see cref="SharedGameDetailResults"/>.</param>
    public static void RecordSharedGameDetailRequest(string result)
    {
        var tags = new TagList { { "result", result } };
        SharedGameDetailRequestsTotal.Add(1, tags);
    }

    /// <summary>
    /// Records a cache lookup outcome.
    /// </summary>
    /// <param name="source">One of <see cref="CacheHitSources"/>.</param>
    public static void RecordSharedGameDetailCacheHit(string source)
    {
        var tags = new TagList { { "source", source } };
        SharedGameDetailCacheHitsTotal.Add(1, tags);
    }

    /// <summary>
    /// Records the end-to-end render duration of the handler.
    /// </summary>
    /// <param name="elapsedSeconds">Wall-clock elapsed time in seconds.</param>
    /// <param name="cacheOutcome">One of <see cref="SharedGameDetailCacheOutcomes"/>.</param>
    public static void RecordSharedGameDetailRenderDuration(double elapsedSeconds, string cacheOutcome)
    {
        var tags = new TagList { { "cache_outcome", cacheOutcome } };
        SharedGameDetailRenderDurationSeconds.Record(elapsedSeconds, tags);
    }

    /// <summary>
    /// Records a single cross-bounded-context fan-out query duration.
    /// </summary>
    /// <param name="elapsedSeconds">Wall-clock elapsed time in seconds.</param>
    /// <param name="boundedContext">One of <see cref="SharedGameDetailBoundedContexts"/>.</param>
    public static void RecordSharedGameDetailCrossBcQuery(double elapsedSeconds, string boundedContext)
    {
        var tags = new TagList { { "bounded_context", boundedContext } };
        SharedGameDetailCrossBcQueryDurationSeconds.Record(elapsedSeconds, tags);
    }
}

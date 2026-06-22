// Issue #1292 — Gamebook index empty-response monitoring.
// Tracks empty-state production occurrences to detect regressions on the
// `/api/v1/gamebooks` cross-aggregate query introduced by #1288.
//
// Cardinality budget: 3 reason labels only. No user_id or other high-cardinality
// labels per AC-6.7.
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    #region Gamebook Index Metrics (Issue #1292)

    /// <summary>
    /// Counter for total empty responses on GET /api/v1/gamebooks.
    /// Labels: reason (no_entries / filter_too_strict / backend_error).
    /// </summary>
    public static readonly Counter<long> GamebookIndexEmptyResponsesTotal = Meter.CreateCounter<long>(
        name: "meepleai.gamebook.index_empty_responses_total",
        unit: "responses",
        description: "Total empty responses on GET /api/v1/gamebooks by reason classification");

    /// <summary>
    /// Histogram for warm-up duration per user in seconds.
    /// Labels: outcome (success/failure).
    /// </summary>
    public static readonly Histogram<double> GamebookCacheWarmupDurationSeconds = Meter.CreateHistogram<double>(
        name: "meepleai.gamebook.cache_warmup_duration_seconds",
        unit: "s",
        description: "Warm-up duration per dogfood user in seconds, by outcome");

    /// <summary>
    /// Counter for total warm-up attempts by outcome.
    /// Labels: outcome (success/failure).
    /// </summary>
    public static readonly Counter<long> GamebookCacheWarmupTotal = Meter.CreateCounter<long>(
        name: "meepleai.gamebook.cache_warmup_total",
        unit: "attempts",
        description: "Total warm-up attempts for dogfood users by outcome");

    #endregion
}

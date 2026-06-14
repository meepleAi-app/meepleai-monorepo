// Issue #2097 / ADR-060 — Live Session persistence observability
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    // ── Live Session persistence (Issue #2097 / ADR-060) ─────────────────────

    /// <summary>
    /// Total LiveSession write operations to the database.
    ///
    /// Tags:
    ///   • <c>op</c> — "create" (AddAsync) | "update" (UpdateAsync)
    ///
    /// Cardinality: 2 series.
    /// </summary>
    public static readonly Counter<long> LiveSessionWritesTotal = Meter.CreateCounter<long>(
        name: "meepleai.live_session.writes.total",
        unit: "operations",
        description: "Total LiveSession write operations to the database, tagged by op (create|update).");

    /// <summary>
    /// Wall-clock duration of <c>LiveSessionRepository.UpdateAsync</c>, covering
    /// the full synchronous path: mapper, 4 child-collection sync queries (Players,
    /// Teams, RoundScores, TurnRecords), and EF change-tracker state mutations.
    /// Makes the N+1 child sync overhead visible (issue I-3 Phase 3 review).
    ///
    /// Tags: none (single histogram; cardinality = 1).
    ///
    /// Suggested alert: p99 &gt; 200 ms → child collection fan-out degradation.
    /// </summary>
    public static readonly Histogram<double> LiveSessionUpdateDurationSeconds = Meter.CreateHistogram<double>(
        name: "meepleai.live_session.update.duration",
        unit: "s",
        description: "Wall-clock duration of LiveSessionRepository.UpdateAsync (mapper + 4 child-sync queries).");
}

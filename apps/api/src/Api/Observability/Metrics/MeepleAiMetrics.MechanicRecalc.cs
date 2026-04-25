// ADR-051 Sprint 2 / Task 8: Mechanic-recalc background worker metrics.
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    #region MechanicRecalc Metrics (ADR-051 Sprint 2 / Task 8)

    /// <summary>
    /// Counter for total <see cref="BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicRecalcJob"/>
    /// runs that reached a terminal status. Tagged with <c>status</c>
    /// (<c>Completed</c>|<c>Failed</c>|<c>Cancelled</c>) for slice-by-outcome dashboards.
    /// </summary>
    public static readonly Counter<long> JobsCompleted = Meter.CreateCounter<long>(
        name: "meepleai.mechanic_recalc.jobs_completed",
        unit: "events",
        description: "Total mechanic-recalc jobs that reached a terminal status.");

    /// <summary>
    /// Counter for individual mechanic-analyses successfully recomputed by the recalc worker.
    /// Excludes skips (analyses no longer eligible) and failures
    /// (see <see cref="AnalysesFailed"/>).
    /// </summary>
    public static readonly Counter<long> AnalysesProcessed = Meter.CreateCounter<long>(
        name: "meepleai.mechanic_recalc.analyses_processed",
        unit: "events",
        description: "Mechanic-analyses successfully recomputed by the recalc worker.");

    /// <summary>
    /// Counter for individual mechanic-analyses that raised an unexpected exception during
    /// recomputation. Skips (NotFound/Conflict) are not counted here.
    /// </summary>
    public static readonly Counter<long> AnalysesFailed = Meter.CreateCounter<long>(
        name: "meepleai.mechanic_recalc.analyses_failed",
        unit: "events",
        description: "Mechanic-analyses that raised an unexpected exception during recomputation.");

    /// <summary>
    /// Counter for circuit-breaker trips inside the recalc worker — incremented every time the
    /// per-job <c>ConsecutiveFailures</c> threshold (5) is reached and the worker short-circuits
    /// the remaining iterations by failing the job.
    /// </summary>
    public static readonly Counter<long> CircuitBreakerOpens = Meter.CreateCounter<long>(
        name: "meepleai.mechanic_recalc.circuit_breaker_opens",
        unit: "events",
        description: "Recalc-worker circuit-breaker trips (consecutive failures >= 5).");

    #endregion
}

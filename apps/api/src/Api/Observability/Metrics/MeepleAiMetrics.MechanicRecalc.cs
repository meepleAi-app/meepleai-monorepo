// ADR-051 Sprint 2 / Task 8 + Task 11: Mechanic-recalc background worker metrics.
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    #region MechanicRecalc Metrics (ADR-051 Sprint 2 / Task 8 + Task 11)

    /// <summary>
    /// Counter for <see cref="BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicRecalcJob"/>
    /// instances that have been enqueued via
    /// <see cref="BoundedContexts.SharedGameCatalog.Application.Commands.Validation.EnqueueRecalculateAllMechanicMetricsCommand"/>.
    /// Incremented from the command handler immediately after <c>SaveChangesAsync</c>; this counter
    /// crossed with <see cref="JobsCompleted"/> reveals queue backlog and orphaned-job rate.
    /// </summary>
    public static readonly Counter<long> JobsEnqueued = Meter.CreateCounter<long>(
        name: "meepleai.mechanic_recalc.jobs_enqueued",
        unit: "events",
        description: "Total mechanic-recalc jobs that have been persisted in Pending status.");

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

    /// <summary>
    /// Histogram for total wall-clock duration (in seconds) of a recalc job — measured from
    /// <c>StartedAt</c> (set by <c>ClaimNextPendingAsync</c>) to the moment the worker records the
    /// terminal transition. Tagged with <c>status</c> for slicing by outcome.
    /// </summary>
    public static readonly Histogram<double> JobDuration = Meter.CreateHistogram<double>(
        name: "meepleai.mechanic_recalc.job_duration",
        unit: "s",
        description: "Wall-clock duration of a recalc job from claim to terminal transition.");

    #endregion
}

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// Lifecycle status of a <see cref="Aggregates.MechanicRecalcJob"/> (ADR-051 M2.1).
/// Integer codes are stored as-is in the <c>status</c> column of <c>mechanic_recalc_jobs</c>.
/// </summary>
/// <remarks>
/// State machine (transitions enforced by the aggregate):
/// <code>
///   Pending (0) ──MarkRunning──────► Running (1)
///   Running (1) ──Complete──────────► Completed (2)
///   Pending (0) ──Fail──────────────► Failed (3)
///   Running (1) ──Fail──────────────► Failed (3)
///   Cancelled (4) is currently unused as a terminal status; the worker uses Completed/Failed
///   after honouring the CancellationRequested flag.
/// </code>
/// </remarks>
public enum RecalcJobStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Aggregate root for a single background mechanic-recalculation run (ADR-051 M2.1, Sprint 2).
/// Tracks the lifecycle of an admin-triggered job that iterates over all Published analyses and
/// re-evaluates their certification metrics.
/// </summary>
/// <remarks>
/// Lifecycle state machine:
/// <code>
///   Pending (0) ──MarkRunning──► Running (1)
///   Running (1) ──Complete──────► Completed (2)
///   Running (1) ──Fail──────────► Failed (3)
///   Pending (0) ──Fail──────────► Failed (3)     (pipeline error before pickup)
///   Pending (0) ──RequestCancellation──► Pending  (flag set; worker honours it on pickup)
///   Running (1) ──RequestCancellation──► Running  (flag set; worker short-circuits)
/// </code>
/// Cancellation is a flag, not a status. The worker transitions to Completed or Failed after
/// honouring the flag. Complete/Fail are terminal — no further transitions are allowed.
///
/// Circuit-breaker note: <see cref="ConsecutiveFailures"/> reaching ≥ 5 does NOT auto-Fail the
/// job here; it is left in Running so the worker (Task 8) can decide whether to short-circuit.
/// </remarks>
public sealed class MechanicRecalcJob : AggregateRoot<Guid>
{
    // === Status ===

    public RecalcJobStatus Status { get; private set; }

    // === Identity / trigger ===

    public Guid TriggeredByUserId { get; private set; }

    // === Progress counters ===

    public int Total { get; private set; }
    public int Processed { get; private set; }
    public int Failed { get; private set; }
    public int Skipped { get; private set; }

    // === Circuit-breaker ===

    public int ConsecutiveFailures { get; private set; }
    public string? LastError { get; private set; }
    public Guid? LastProcessedAnalysisId { get; private set; }

    // === Cancellation ===

    public bool CancellationRequested { get; private set; }

    // === Timestamps ===

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? HeartbeatAt { get; private set; }

    // ===================================================
    // Constructors
    // ===================================================

    /// <summary>EF Core / repository reconstitution — do not call directly.</summary>
    private MechanicRecalcJob() : base()
    {
    }

    private MechanicRecalcJob(Guid id, Guid triggeredByUserId, DateTimeOffset createdAt)
        : base(id)
    {
        TriggeredByUserId = triggeredByUserId;
        CreatedAt = createdAt;
        Status = RecalcJobStatus.Pending;
    }

    // ===================================================
    // Factory
    // ===================================================

    /// <summary>
    /// Creates a new recalc job in <see cref="RecalcJobStatus.Pending"/> status.
    /// </summary>
    /// <param name="triggeredBy">The admin user who requested the recalculation.</param>
    /// <returns>A new <see cref="MechanicRecalcJob"/> ready to be persisted.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="triggeredBy"/> is empty.</exception>
    public static MechanicRecalcJob Enqueue(Guid triggeredBy)
    {
        if (triggeredBy == Guid.Empty)
        {
            throw new ArgumentException("TriggeredByUserId cannot be empty.", nameof(triggeredBy));
        }

        return new MechanicRecalcJob(
            id: Guid.NewGuid(),
            triggeredByUserId: triggeredBy,
            createdAt: DateTimeOffset.UtcNow);
    }

    // ===================================================
    // Lifecycle transitions
    // ===================================================

    /// <summary>
    /// Transitions the job from <see cref="RecalcJobStatus.Pending"/> to
    /// <see cref="RecalcJobStatus.Running"/>. Sets the total analysis count and stamps
    /// <see cref="StartedAt"/>.
    /// </summary>
    /// <param name="total">Number of analyses the worker will attempt to process.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="total"/> is negative.</exception>
    /// <exception cref="InvalidMechanicRecalcJobTransitionException">
    /// Thrown if the job is not currently in <see cref="RecalcJobStatus.Pending"/>.
    /// </exception>
    public void MarkRunning(int total)
    {
        if (total < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(total), total, "Total must be non-negative.");
        }

        RequireStatus("mark running", RecalcJobStatus.Pending);

        Status = RecalcJobStatus.Running;
        Total = total;
        StartedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Records a successful analysis evaluation. Increments <see cref="Processed"/>, resets
    /// <see cref="ConsecutiveFailures"/> to zero, and updates <see cref="LastProcessedAnalysisId"/>.
    /// </summary>
    /// <exception cref="InvalidMechanicRecalcJobTransitionException">
    /// Thrown if the job is not currently <see cref="RecalcJobStatus.Running"/>.
    /// </exception>
    public void RecordSuccess(Guid analysisId)
    {
        RequireStatus("record success", RecalcJobStatus.Running);

        Processed++;
        ConsecutiveFailures = 0;
        LastProcessedAnalysisId = analysisId;
    }

    /// <summary>
    /// Records a failed analysis evaluation. Increments both <see cref="Failed"/> and
    /// <see cref="ConsecutiveFailures"/>, and captures the error message in <see cref="LastError"/>.
    /// </summary>
    /// <remarks>
    /// Reaching <see cref="ConsecutiveFailures"/> ≥ 5 does NOT auto-fail the job — the worker
    /// (Task 8) is responsible for checking the circuit-breaker threshold and calling
    /// <see cref="Fail"/> if appropriate.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown if <paramref name="error"/> is null or whitespace.</exception>
    /// <exception cref="InvalidMechanicRecalcJobTransitionException">
    /// Thrown if the job is not currently <see cref="RecalcJobStatus.Running"/>.
    /// </exception>
    public void RecordFailure(Guid analysisId, string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            throw new ArgumentException("Error message is required.", nameof(error));
        }

        RequireStatus("record failure", RecalcJobStatus.Running);

        Failed++;
        ConsecutiveFailures++;
        LastError = error;
        LastProcessedAnalysisId = analysisId;
    }

    /// <summary>
    /// Records that an analysis was skipped (e.g., already certified, not eligible).
    /// Increments <see cref="Skipped"/> without touching <see cref="ConsecutiveFailures"/>.
    /// </summary>
    /// <exception cref="InvalidMechanicRecalcJobTransitionException">
    /// Thrown if the job is not currently <see cref="RecalcJobStatus.Running"/>.
    /// </exception>
    public void RecordSkip()
    {
        RequireStatus("record skip", RecalcJobStatus.Running);
        Skipped++;
    }

    /// <summary>
    /// Sets the cancellation-requested flag. Allowed from <see cref="RecalcJobStatus.Pending"/>
    /// or <see cref="RecalcJobStatus.Running"/>. Idempotent — calling twice is not an error.
    /// </summary>
    /// <exception cref="InvalidMechanicRecalcJobTransitionException">
    /// Thrown if the job is in a terminal state (Completed, Failed, or Cancelled).
    /// </exception>
    public void RequestCancellation()
    {
        if (Status is RecalcJobStatus.Completed or RecalcJobStatus.Failed or RecalcJobStatus.Cancelled)
        {
            throw new InvalidMechanicRecalcJobTransitionException(
                Id,
                Status,
                "request cancellation",
                RecalcJobStatus.Pending,
                RecalcJobStatus.Running);
        }

        CancellationRequested = true;
    }

    /// <summary>
    /// Transitions a <see cref="RecalcJobStatus.Running"/> job to
    /// <see cref="RecalcJobStatus.Completed"/> and stamps <see cref="CompletedAt"/>.
    /// </summary>
    /// <exception cref="InvalidMechanicRecalcJobTransitionException">
    /// Thrown if the job is not currently <see cref="RecalcJobStatus.Running"/>.
    /// </exception>
    public void Complete()
    {
        RequireStatus("complete", RecalcJobStatus.Running);

        Status = RecalcJobStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Transitions a <see cref="RecalcJobStatus.Pending"/> or <see cref="RecalcJobStatus.Running"/>
    /// job to <see cref="RecalcJobStatus.Failed"/>, capturing the failure reason in
    /// <see cref="LastError"/> and stamping <see cref="CompletedAt"/>.
    /// </summary>
    /// <param name="reason">Human-readable reason for the failure (e.g., circuit-breaker trip, unhandled exception).</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="reason"/> is null or whitespace.</exception>
    /// <exception cref="InvalidMechanicRecalcJobTransitionException">
    /// Thrown if the job is not in <see cref="RecalcJobStatus.Pending"/> or <see cref="RecalcJobStatus.Running"/>.
    /// </exception>
    public void Fail(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Failure reason is required.", nameof(reason));
        }

        if (Status is not (RecalcJobStatus.Pending or RecalcJobStatus.Running))
        {
            throw new InvalidMechanicRecalcJobTransitionException(
                Id,
                Status,
                "fail",
                RecalcJobStatus.Pending,
                RecalcJobStatus.Running);
        }

        Status = RecalcJobStatus.Failed;
        LastError = reason;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Updates <see cref="HeartbeatAt"/> to the current UTC time. Only valid while the job is
    /// <see cref="RecalcJobStatus.Running"/> — heartbeats on non-running jobs are a caller error.
    /// </summary>
    /// <exception cref="InvalidMechanicRecalcJobTransitionException">
    /// Thrown if the job is not currently <see cref="RecalcJobStatus.Running"/>.
    /// </exception>
    public void Heartbeat()
    {
        RequireStatus("heartbeat", RecalcJobStatus.Running);
        HeartbeatAt = DateTimeOffset.UtcNow;
    }

    // ===================================================
    // Helpers
    // ===================================================

    private void RequireStatus(string operation, RecalcJobStatus required)
    {
        if (Status != required)
        {
            throw new InvalidMechanicRecalcJobTransitionException(Id, Status, operation, required);
        }
    }
}

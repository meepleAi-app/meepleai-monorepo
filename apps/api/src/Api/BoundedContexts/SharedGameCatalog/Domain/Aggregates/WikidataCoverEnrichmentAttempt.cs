using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Outcome category of a single Wikidata cover enrichment attempt. Tagged onto
/// each <see cref="WikidataCoverEnrichmentAttempt"/> row so the M13 admin
/// dead-letter visibility page + the M11 Prometheus metric expansion can
/// aggregate by terminal state.
/// </summary>
public enum WikidataCoverEnrichmentOutcome
{
    /// <summary>Pipeline completed successfully — cover persisted on the aggregate.</summary>
    Success = 0,

    /// <summary>Pipeline short-circuited for a business reason (qid-missing, image-not-available-p18, license-not-whitelisted, already-enriched-recent, image-bytes-not-available). Terminal — no retry will be scheduled.</summary>
    Skipped = 1,

    /// <summary>Pipeline threw a technical failure (image-processing-error, r2-upload-error). Retry-eligible if <c>NextRetryAt</c> is set; otherwise terminal.</summary>
    Failed = 2,

    /// <summary>Maximum retry count exhausted OR an outcome that is permanently unrecoverable. Terminal — surfaced on the M13 admin dead-letter page.</summary>
    DeadLetter = 3,
}

/// <summary>
/// Aggregate root for a single Wikidata cover enrichment attempt outcome.
/// Issue #1823 Wave 3 M9. Distinct aggregate from the BGG <c>EnrichmentAttempt</c>
/// (#1874) because the two enrichment pipelines have different retry semantics:
/// BGG attempts have no backoff schedule (immediate manual re-queue) while
/// Wikidata attempts use the DEC-3j exponential backoff (1m / 5m / 15m).
/// Newman's deferred-decision verdict in the spec-panel review.
/// </summary>
/// <remarks>
/// <para>Record-of-fact pattern: each row is immutable post-creation. Retry
/// scheduling produces a NEW <c>Failed</c> attempt with <c>NextRetryAt</c>
/// populated; dead-lettering produces a NEW <c>DeadLetter</c> attempt that
/// terminates the chain. This avoids in-place state mutation and gives the M13
/// admin page a complete audit trail of every pipeline iteration per game.</para>
///
/// <para>Lifecycle (per game):
/// <list type="bullet">
///   <item>attempt 1: <c>Failed(reason="r2-upload-error", RetryCount=0, NextRetryAt=+1m)</c></item>
///   <item>attempt 2: <c>Failed(reason="r2-upload-error", RetryCount=1, NextRetryAt=+5m)</c></item>
///   <item>attempt 3: <c>Failed(reason="r2-upload-error", RetryCount=2, NextRetryAt=+15m)</c></item>
///   <item>attempt 4 (after retry-3 exhaustion): <c>DeadLetter(reason="r2-upload-error", RetryCount=3, DeadLetteredAt=now)</c></item>
/// </list>
/// </para>
/// </remarks>
public sealed class WikidataCoverEnrichmentAttempt : AggregateRoot<Guid>
{
    /// <summary>Target shared game; never <see cref="Guid.Empty"/>.</summary>
    public Guid SharedGameId { get; private set; }

    public DateTime AttemptedAt { get; private set; }

    public WikidataCoverEnrichmentOutcome Outcome { get; private set; }

    /// <summary>Stable machine-readable reason (e.g. <c>"qid-missing"</c>, <c>"r2-upload-error"</c>).</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Optional human-readable detail (typically an exception message). Null on success / qid-missing-style skips.</summary>
    public string? Details { get; private set; }

    /// <summary>0 on the first attempt, N on the (N+1)-th re-run.</summary>
    public int RetryCount { get; private set; }

    /// <summary>UTC timestamp from which the scheduler may pick this game up again. <see langword="null"/> for terminal outcomes (Success / Skipped / DeadLetter).</summary>
    public DateTime? NextRetryAt { get; private set; }

    /// <summary>UTC timestamp at which the attempt was dead-lettered. Non-null only when <see cref="Outcome"/> is <see cref="WikidataCoverEnrichmentOutcome.DeadLetter"/>.</summary>
    public DateTime? DeadLetteredAt { get; private set; }

    /// <summary>UTC timestamp when an operator acknowledged the dead-letter; <see langword="null"/> when not yet acknowledged or not a dead-letter.</summary>
    public DateTime? AcknowledgedAt { get; private set; }

    /// <summary>
    /// User id of the operator who acknowledged the dead-letter; <see langword="null"/> when not yet acknowledged.
    /// Invariant pair with <see cref="AcknowledgedAt"/>: both fields are non-null iff the row has been acknowledged
    /// (they are written atomically in <see cref="Acknowledge"/> and must be reconstituted together).
    /// </summary>
    public Guid? AcknowledgedBy { get; private set; }

    /// <summary>
    /// Issue #1823 Phase F F6 — user id of the admin who manually triggered this
    /// attempt via M12 (single trigger) or F2 (bulk retry). <see langword="null"/>
    /// when invoked by the M9 scheduler (the default path).
    /// </summary>
    public Guid? TriggeredByAdminUserId { get; private set; }

    /// <summary>EF Core / repository reconstitution — do not call directly.</summary>
    private WikidataCoverEnrichmentAttempt() : base() { }

    private WikidataCoverEnrichmentAttempt(
        Guid id,
        Guid sharedGameId,
        DateTime attemptedAt,
        WikidataCoverEnrichmentOutcome outcome,
        string reason,
        string? details,
        int retryCount,
        DateTime? nextRetryAt,
        DateTime? deadLetteredAt,
        DateTime? acknowledgedAt,
        Guid? acknowledgedBy,
        Guid? triggeredByAdminUserId) : base(id)
    {
        SharedGameId = sharedGameId;
        AttemptedAt = attemptedAt;
        Outcome = outcome;
        Reason = reason;
        Details = details;
        RetryCount = retryCount;
        NextRetryAt = nextRetryAt;
        DeadLetteredAt = deadLetteredAt;
        AcknowledgedAt = acknowledgedAt;
        AcknowledgedBy = acknowledgedBy;
        TriggeredByAdminUserId = triggeredByAdminUserId;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Factories
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Records a successful enrichment. Terminal — no NextRetryAt.</summary>
    public static WikidataCoverEnrichmentAttempt RecordSuccess(
        Guid sharedGameId,
        int retryCount,
        DateTime attemptedAt,
        Guid? triggeredByAdminUserId = null) =>
        Create(sharedGameId, attemptedAt, WikidataCoverEnrichmentOutcome.Success,
            reason: "success", details: null, retryCount: retryCount,
            nextRetryAt: null, deadLetteredAt: null,
            triggeredByAdminUserId: triggeredByAdminUserId);

    /// <summary>Records a business skip (qid-missing, license-not-whitelisted, etc.). Terminal — no retry.</summary>
    public static WikidataCoverEnrichmentAttempt RecordSkipped(
        Guid sharedGameId,
        string reason,
        int retryCount,
        DateTime attemptedAt,
        Guid? triggeredByAdminUserId = null) =>
        Create(sharedGameId, attemptedAt, WikidataCoverEnrichmentOutcome.Skipped,
            reason: reason, details: null, retryCount: retryCount,
            nextRetryAt: null, deadLetteredAt: null,
            triggeredByAdminUserId: triggeredByAdminUserId);

    /// <summary>Records a technical failure with a retry scheduled.</summary>
    public static WikidataCoverEnrichmentAttempt RecordFailedWithRetry(
        Guid sharedGameId,
        string reason,
        string? details,
        int retryCount,
        DateTime attemptedAt,
        DateTime nextRetryAt,
        Guid? triggeredByAdminUserId = null) =>
        Create(sharedGameId, attemptedAt, WikidataCoverEnrichmentOutcome.Failed,
            reason: reason, details: details, retryCount: retryCount,
            nextRetryAt: nextRetryAt, deadLetteredAt: null,
            triggeredByAdminUserId: triggeredByAdminUserId);

    /// <summary>Records a dead-letter — terminal failure after max retries OR unrecoverable error.</summary>
    public static WikidataCoverEnrichmentAttempt RecordDeadLetter(
        Guid sharedGameId,
        string reason,
        string? details,
        int retryCount,
        DateTime attemptedAt,
        Guid? triggeredByAdminUserId = null) =>
        Create(sharedGameId, attemptedAt, WikidataCoverEnrichmentOutcome.DeadLetter,
            reason: reason, details: details, retryCount: retryCount,
            nextRetryAt: null, deadLetteredAt: attemptedAt,
            triggeredByAdminUserId: triggeredByAdminUserId);

    /// <summary>Repository hydration — bypasses invariants. Caller guarantees state validity.</summary>
    public static WikidataCoverEnrichmentAttempt Reconstitute(
        Guid id,
        Guid sharedGameId,
        DateTime attemptedAt,
        WikidataCoverEnrichmentOutcome outcome,
        string reason,
        string? details,
        int retryCount,
        DateTime? nextRetryAt,
        DateTime? deadLetteredAt,
        DateTime? acknowledgedAt,
        Guid? acknowledgedBy,
        Guid? triggeredByAdminUserId) =>
        new(id, sharedGameId, attemptedAt, outcome, reason, details,
            retryCount, nextRetryAt, deadLetteredAt,
            acknowledgedAt, acknowledgedBy, triggeredByAdminUserId);

    private static WikidataCoverEnrichmentAttempt Create(
        Guid sharedGameId,
        DateTime attemptedAt,
        WikidataCoverEnrichmentOutcome outcome,
        string reason,
        string? details,
        int retryCount,
        DateTime? nextRetryAt,
        DateTime? deadLetteredAt,
        Guid? triggeredByAdminUserId)
    {
        if (sharedGameId == Guid.Empty)
            throw new ArgumentException("SharedGameId cannot be Guid.Empty.", nameof(sharedGameId));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        if (reason.Length > 64)
            throw new ArgumentException("Reason must be 64 characters or fewer.", nameof(reason));

        if (details is { Length: > 1024 })
            throw new ArgumentException("Details must be 1024 characters or fewer.", nameof(details));

        if (retryCount < 0)
            throw new ArgumentOutOfRangeException(nameof(retryCount), retryCount, "RetryCount must be non-negative.");

        return new WikidataCoverEnrichmentAttempt(
            id: Guid.NewGuid(),
            sharedGameId: sharedGameId,
            attemptedAt: attemptedAt,
            outcome: outcome,
            reason: reason,
            details: details,
            retryCount: retryCount,
            nextRetryAt: nextRetryAt,
            deadLetteredAt: deadLetteredAt,
            acknowledgedAt: null,
            acknowledgedBy: null,
            triggeredByAdminUserId: triggeredByAdminUserId);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Mutators
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Issue #1823 Phase F F5 — operator marks a dead-letter as "not actionable".
    /// EXCEPTION to the record-of-fact pattern documented on the type: ack is
    /// operational metadata orthogonal to the pipeline (parallel to
    /// <see cref="DeadLetteredAt"/>), not a new pipeline event. Idempotent on
    /// re-call: the first ack is preserved and subsequent calls are no-ops so
    /// repeated bulk-acknowledge clicks cannot rewrite the audit trail.
    /// </summary>
    /// <param name="userId">Acknowledging admin user id; must not be <see cref="Guid.Empty"/>.</param>
    /// <param name="ackedAt">UTC acknowledgement timestamp.</param>
    public void Acknowledge(Guid userId, DateTime ackedAt)
    {
        if (Outcome != WikidataCoverEnrichmentOutcome.DeadLetter)
            throw new InvalidOperationException(
                $"Only DeadLetter attempts can be acknowledged; current Outcome={Outcome}.");

        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be Guid.Empty.", nameof(userId));

        if (AcknowledgedAt is not null) return; // idempotent

        AcknowledgedAt = ackedAt;
        AcknowledgedBy = userId;
    }
}

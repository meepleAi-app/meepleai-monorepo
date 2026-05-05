using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Aggregate root for the AI-first Mechanic Extractor pipeline (ADR-051). Represents a single
/// AI-generated analysis of a game's rulebook PDF, with a lifecycle state machine, per-claim
/// review, suppression kill-switch (T5), and cost cap governance (T8).
/// </summary>
/// <remarks>
/// Lifecycle state machine:
/// <code>
///   Draft (0) ──SubmitForReview──► InReview (1)
///   InReview (1) ──Approve──────► Published (2)   (requires all claims Approved)
///   InReview (1) ──Reject───────► Rejected (3)
///   Rejected (3) ──SubmitForReview──► InReview (1) (re-submit after edits)
/// </code>
/// Suppression (<see cref="IsSuppressed"/>) is orthogonal to <see cref="Status"/>: a Published
/// analysis may be suppressed without changing status. Global query filter hides suppressed rows
/// from player-facing queries.
/// </remarks>
public sealed class MechanicAnalysis : AggregateRoot<Guid>
{
    private readonly List<MechanicClaim> _claims = new();

    private static readonly Dictionary<MechanicAnalysisStatus, MechanicAnalysisStatus[]> ValidTransitions = new()
    {
        [MechanicAnalysisStatus.Draft] = new[] { MechanicAnalysisStatus.InReview },
        [MechanicAnalysisStatus.InReview] = new[] { MechanicAnalysisStatus.Published, MechanicAnalysisStatus.Rejected },
        [MechanicAnalysisStatus.Rejected] = new[] { MechanicAnalysisStatus.InReview },
        [MechanicAnalysisStatus.PartiallyExtracted] = new[] { MechanicAnalysisStatus.InReview, MechanicAnalysisStatus.Rejected },
        [MechanicAnalysisStatus.Published] = Array.Empty<MechanicAnalysisStatus>()
    };

    /// <summary>
    /// System-initiated rejection reasons, used by <see cref="AutoRejectFromDraft"/>.
    /// Distinguishes automated failures (cost cap, LLM catastrophic) from human-initiated
    /// rejections during review.
    /// </summary>
    /// <remarks>
    /// <see cref="PipelineCrashed"/> is reserved for unexpected exceptions that escape the
    /// pipeline's own error handling (DB error, OOM, network timeout, validation crash, etc.).
    /// LLM-specific failures are surfaced by the pipeline as
    /// <c>MechanicPipelineOutcome.AbortedLlmFailed</c> and mapped to <see cref="LlmGenerationFailed"/>
    /// — they never reach the unexpected-failure path. Issue #597.
    /// </remarks>
    public static class AutoRejectionReasons
    {
        public const string CostCapExceeded = "cost_cap_exceeded";
        public const string LlmGenerationFailed = "llm_generation_failed";
        public const string ValidationFailedBeyondRetry = "validation_failed_beyond_retry";
        public const string PipelineCrashed = "pipeline_crashed";
    }

    // === Core identity / lineage ===

    public Guid SharedGameId { get; private set; }
    public Guid PdfDocumentId { get; private set; }
    public string PromptVersion { get; private set; } = string.Empty;

    // === Lifecycle ===

    public MechanicAnalysisStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    // === LLM execution snapshot ===

    public int TotalTokensUsed { get; private set; }
    public decimal EstimatedCostUsd { get; private set; }
    public string ModelUsed { get; private set; } = string.Empty;
    public string Provider { get; private set; } = string.Empty;

    // === T8 cost governance (ADR-051) ===

    public decimal CostCapUsd { get; private set; }
    public DateTime? CostCapOverrideAt { get; private set; }
    public Guid? CostCapOverrideBy { get; private set; }
    public string? CostCapOverrideReason { get; private set; }

    // === T5 takedown kill-switch ===

    public bool IsSuppressed { get; private set; }
    public DateTime? SuppressedAt { get; private set; }
    public Guid? SuppressedBy { get; private set; }
    public string? SuppressionReason { get; private set; }
    public DateTime? SuppressionRequestedAt { get; private set; }
    public SuppressionRequestSource? SuppressionRequestSource { get; private set; }

    // === Children ===

    public IReadOnlyList<MechanicClaim> Claims => _claims.AsReadOnly();

    // === AI comprehension certification (ADR-051 M2) ===

    public CertificationStatus CertificationStatus { get; private set; } = CertificationStatus.NotEvaluated;
    public DateTimeOffset? CertifiedAt { get; private set; }
    public Guid? CertifiedByUserId { get; private set; }
    public string? CertificationOverrideReason { get; private set; }
    public Guid? LastMetricsId { get; private set; }

    /// <summary>
    /// Optimistic concurrency token — PostgreSQL's system <c>xmin</c> column value as seen at
    /// load time. Round-tripped through the aggregate so detached <c>DbContext.Update</c> in the
    /// repository preserves the original token (instead of defaulting to 0 and triggering a
    /// spurious concurrency exception on every save).
    /// </summary>
    public uint XminVersion { get; private set; }

    /// <summary>EF Core constructor / repository reconstitution.</summary>
    private MechanicAnalysis() : base()
    {
    }

    private MechanicAnalysis(
        Guid id,
        Guid sharedGameId,
        Guid pdfDocumentId,
        string promptVersion,
        Guid createdBy,
        DateTime createdAt,
        string modelUsed,
        string provider,
        decimal costCapUsd)
        : base(id)
    {
        SharedGameId = sharedGameId;
        PdfDocumentId = pdfDocumentId;
        PromptVersion = promptVersion;
        Status = MechanicAnalysisStatus.Draft;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        ModelUsed = modelUsed;
        Provider = provider;
        CostCapUsd = costCapUsd;
        IsSuppressed = false;
        TotalTokensUsed = 0;
        EstimatedCostUsd = 0m;
    }

    /// <summary>
    /// Creates a new analysis in <see cref="MechanicAnalysisStatus.Draft"/> status with the
    /// initial set of claims produced by the AI pipeline. The claims list can be empty if the
    /// caller plans to populate it via subsequent calls (but the state will not be valid until
    /// at least one claim exists).
    /// </summary>
    public static MechanicAnalysis Create(
        Guid sharedGameId,
        Guid pdfDocumentId,
        string promptVersion,
        Guid createdBy,
        DateTime createdAt,
        string modelUsed,
        string provider,
        decimal costCapUsd,
        IEnumerable<MechanicClaim>? initialClaims = null)
    {
        if (sharedGameId == Guid.Empty)
        {
            throw new ArgumentException("SharedGameId cannot be empty.", nameof(sharedGameId));
        }

        if (pdfDocumentId == Guid.Empty)
        {
            throw new ArgumentException("PdfDocumentId cannot be empty.", nameof(pdfDocumentId));
        }

        if (string.IsNullOrWhiteSpace(promptVersion))
        {
            throw new ArgumentException("PromptVersion is required for reproducibility (ADR-051 T7).", nameof(promptVersion));
        }

        if (createdBy == Guid.Empty)
        {
            throw new ArgumentException("CreatedBy cannot be empty.", nameof(createdBy));
        }

        if (string.IsNullOrWhiteSpace(modelUsed))
        {
            throw new ArgumentException("ModelUsed is required.", nameof(modelUsed));
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("Provider is required.", nameof(provider));
        }

        if (costCapUsd <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(costCapUsd),
                costCapUsd,
                "CostCapUsd must be strictly positive (ADR-051 T8).");
        }

        var analysis = new MechanicAnalysis(
            id: Guid.NewGuid(),
            sharedGameId: sharedGameId,
            pdfDocumentId: pdfDocumentId,
            promptVersion: promptVersion.Trim(),
            createdBy: createdBy,
            createdAt: createdAt,
            modelUsed: modelUsed.Trim(),
            provider: provider.Trim(),
            costCapUsd: costCapUsd);

        if (initialClaims is not null)
        {
            foreach (var claim in initialClaims)
            {
                analysis._claims.Add(claim);
            }
        }

        return analysis;
    }

    /// <summary>
    /// Rehydrates an aggregate from persistence. Used exclusively by the repository's
    /// <c>MapToDomain</c>; bypasses validation because invariants were already enforced when
    /// the aggregate was originally created. Does NOT raise domain events.
    /// </summary>
    /// <remarks>
    /// Pre-loaded <paramref name="claims"/> are attached as-is; the repository is responsible
    /// for materializing the full graph (Claim + Citations) before calling this factory.
    /// </remarks>
    public static MechanicAnalysis Reconstitute(
        Guid id,
        Guid sharedGameId,
        Guid pdfDocumentId,
        string promptVersion,
        MechanicAnalysisStatus status,
        Guid createdBy,
        DateTime createdAt,
        Guid? reviewedBy,
        DateTime? reviewedAt,
        string? rejectionReason,
        int totalTokensUsed,
        decimal estimatedCostUsd,
        string modelUsed,
        string provider,
        decimal costCapUsd,
        DateTime? costCapOverrideAt,
        Guid? costCapOverrideBy,
        string? costCapOverrideReason,
        bool isSuppressed,
        DateTime? suppressedAt,
        Guid? suppressedBy,
        string? suppressionReason,
        DateTime? suppressionRequestedAt,
        SuppressionRequestSource? suppressionRequestSource,
        IEnumerable<MechanicClaim> claims,
        CertificationStatus certificationStatus = CertificationStatus.NotEvaluated,
        DateTimeOffset? certifiedAt = null,
        Guid? certifiedByUserId = null,
        string? certificationOverrideReason = null,
        Guid? lastMetricsId = null,
        uint xminVersion = 0)
    {
        ArgumentNullException.ThrowIfNull(claims);

        var analysis = new MechanicAnalysis
        {
            XminVersion = xminVersion,
            // Base Entity<Guid>.Id has a protected setter, accessible from this derived type.
            Id = id,
            SharedGameId = sharedGameId,
            PdfDocumentId = pdfDocumentId,
            PromptVersion = promptVersion,
            Status = status,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ReviewedBy = reviewedBy,
            ReviewedAt = reviewedAt,
            RejectionReason = rejectionReason,
            TotalTokensUsed = totalTokensUsed,
            EstimatedCostUsd = estimatedCostUsd,
            ModelUsed = modelUsed,
            Provider = provider,
            CostCapUsd = costCapUsd,
            CostCapOverrideAt = costCapOverrideAt,
            CostCapOverrideBy = costCapOverrideBy,
            CostCapOverrideReason = costCapOverrideReason,
            IsSuppressed = isSuppressed,
            SuppressedAt = suppressedAt,
            SuppressedBy = suppressedBy,
            SuppressionReason = suppressionReason,
            SuppressionRequestedAt = suppressionRequestedAt,
            SuppressionRequestSource = suppressionRequestSource,
            CertificationStatus = certificationStatus,
            CertifiedAt = certifiedAt,
            CertifiedByUserId = certifiedByUserId,
            CertificationOverrideReason = certificationOverrideReason,
            LastMetricsId = lastMetricsId
        };

        foreach (var claim in claims)
        {
            analysis._claims.Add(claim);
        }

        return analysis;
    }

    /// <summary>
    /// Adds a claim to a Draft analysis. Useful for incremental generation (one section at a time).
    /// </summary>
    public void AddClaim(MechanicClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);

        if (Status != MechanicAnalysisStatus.Draft)
        {
            throw new InvalidMechanicAnalysisStateException(
                Id,
                Status,
                "add claim",
                MechanicAnalysisStatus.Draft);
        }

        if (claim.AnalysisId != Id)
        {
            throw new ArgumentException(
                $"Claim's AnalysisId ({claim.AnalysisId}) does not match this analysis ({Id}).",
                nameof(claim));
        }

        _claims.Add(claim);
    }

    /// <summary>
    /// Records the LLM usage snapshot (tokens + actual cost) after generation completes.
    /// Callable only in Draft — once submitted for review the snapshot is frozen.
    /// </summary>
    public void RecordUsage(int totalTokensUsed, decimal estimatedCostUsd)
    {
        if (Status != MechanicAnalysisStatus.Draft)
        {
            throw new InvalidMechanicAnalysisStateException(
                Id,
                Status,
                "record LLM usage",
                MechanicAnalysisStatus.Draft);
        }

        if (totalTokensUsed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalTokensUsed), totalTokensUsed, "Must be non-negative.");
        }

        if (estimatedCostUsd < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedCostUsd), estimatedCostUsd, "Must be non-negative.");
        }

        TotalTokensUsed = totalTokensUsed;
        EstimatedCostUsd = estimatedCostUsd;
    }

    // === State transitions ===

    /// <summary>
    /// Submits the analysis for admin review. Allowed from Draft (first submit) or Rejected
    /// (resubmit after edits).
    /// </summary>
    public void SubmitForReview(Guid actorId, DateTime utcNow)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("ActorId cannot be empty.", nameof(actorId));
        }

        EnsureTransitionAllowed("submit for review", MechanicAnalysisStatus.InReview);

        if (_claims.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot submit MechanicAnalysis {Id} for review: no claims have been added.");
        }

        // Resubmission after rejection: reset claim statuses to Pending so the admin re-reviews them.
        if (Status == MechanicAnalysisStatus.Rejected)
        {
            foreach (var claim in _claims)
            {
                claim.ResetToPending();
            }

            RejectionReason = null;
        }

        // Promotion from a system-initiated partial-extraction checkpoint: claims are already Pending
        // (they were never reviewed), so we only clear the abort marker. The admin is now taking
        // ownership of the salvaged claims and putting them through the normal review flow.
        if (Status == MechanicAnalysisStatus.PartiallyExtracted)
        {
            RejectionReason = null;
        }

        var previous = Status;
        Status = MechanicAnalysisStatus.InReview;
        ReviewedBy = null;
        ReviewedAt = null;

        AddDomainEvent(new MechanicAnalysisStatusChangedEvent(Id, previous, Status, actorId, note: null));
    }

    /// <summary>
    /// Approves the analysis and transitions it to Published. Requires every claim to be Approved.
    /// </summary>
    public void Approve(Guid reviewerId, DateTime utcNow)
    {
        if (reviewerId == Guid.Empty)
        {
            throw new ArgumentException("ReviewerId cannot be empty.", nameof(reviewerId));
        }

        EnsureTransitionAllowed("approve", MechanicAnalysisStatus.Published);

        if (_claims.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot approve MechanicAnalysis {Id}: it has no claims.");
        }

        if (_claims.Any(c => c.Status != MechanicClaimStatus.Approved))
        {
            throw new InvalidOperationException(
                $"Cannot approve MechanicAnalysis {Id}: not all claims are Approved " +
                $"(Pending={_claims.Count(c => c.Status == MechanicClaimStatus.Pending)}, " +
                $"Rejected={_claims.Count(c => c.Status == MechanicClaimStatus.Rejected)}).");
        }

        var previous = Status;
        Status = MechanicAnalysisStatus.Published;
        ReviewedBy = reviewerId;
        ReviewedAt = utcNow;

        AddDomainEvent(new MechanicAnalysisStatusChangedEvent(Id, previous, Status, reviewerId, note: null));
    }

    /// <summary>
    /// Rejects the analysis with a reason. The analysis can later be resubmitted via
    /// <see cref="SubmitForReview(Guid, DateTime)"/> after the author edits the claims.
    /// </summary>
    public void Reject(Guid reviewerId, string reason, DateTime utcNow)
    {
        if (reviewerId == Guid.Empty)
        {
            throw new ArgumentException("ReviewerId cannot be empty.", nameof(reviewerId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Rejection reason is required.", nameof(reason));
        }

        EnsureTransitionAllowed("reject", MechanicAnalysisStatus.Rejected);

        var previous = Status;
        Status = MechanicAnalysisStatus.Rejected;
        ReviewedBy = reviewerId;
        ReviewedAt = utcNow;
        RejectionReason = reason.Trim();

        AddDomainEvent(new MechanicAnalysisStatusChangedEvent(Id, previous, Status, reviewerId, RejectionReason));
    }

    /// <summary>
    /// System-initiated rejection from <see cref="MechanicAnalysisStatus.Draft"/>, bypassing the
    /// normal <c>Draft → InReview → Rejected</c> flow. Used by the generation pipeline (M1.2) when
    /// the run aborts mid-generation (cost cap exceeded, catastrophic LLM failure, validation beyond
    /// retry budget). Preserves any partial claims generated so far as evidence.
    /// </summary>
    /// <param name="reason">One of <see cref="AutoRejectionReasons"/> constants; free-form strings
    /// are accepted for forward-compatibility but discouraged.</param>
    /// <param name="actorId">The admin who initiated the generation run; recorded as reviewer for
    /// audit consistency (T6).</param>
    /// <param name="utcNow">Timestamp for the rejection event.</param>
    /// <exception cref="InvalidMechanicAnalysisStateException">
    /// Thrown if the aggregate is not currently in <see cref="MechanicAnalysisStatus.Draft"/>.
    /// </exception>
    public void AutoRejectFromDraft(string reason, Guid actorId, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Auto-rejection reason is required.", nameof(reason));
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("ActorId cannot be empty.", nameof(actorId));
        }

        if (Status != MechanicAnalysisStatus.Draft)
        {
            throw new InvalidMechanicAnalysisStateException(
                Id,
                Status,
                "auto-reject from draft",
                MechanicAnalysisStatus.Draft);
        }

        var previous = Status;
        Status = MechanicAnalysisStatus.Rejected;
        ReviewedBy = actorId;
        ReviewedAt = utcNow;
        RejectionReason = reason.Trim();

        AddDomainEvent(new MechanicAnalysisStatusChangedEvent(Id, previous, Status, actorId, RejectionReason));
    }

    /// <summary>
    /// System-initiated checkpoint from <see cref="MechanicAnalysisStatus.Draft"/> when the M1.2
    /// generation pipeline aborts mid-run but at least one section was successfully parsed before
    /// the abort. Distinct from <see cref="AutoRejectFromDraft"/>: surviving claims are preserved
    /// so admins can triage what was extracted instead of having to re-run the whole pipeline.
    /// Added in ADR-051 Sprint 2 — relaxes the executor's original all-or-nothing atomicity invariant.
    /// </summary>
    /// <param name="reason">One of <see cref="AutoRejectionReasons"/> constants; the same vocabulary
    /// as <see cref="AutoRejectFromDraft"/> is reused since the failure modes are identical.</param>
    /// <param name="actorId">The admin who initiated the generation run; recorded as reviewer for
    /// audit consistency (T6).</param>
    /// <param name="utcNow">Timestamp for the checkpoint event.</param>
    /// <exception cref="InvalidMechanicAnalysisStateException">
    /// Thrown if the aggregate is not currently in <see cref="MechanicAnalysisStatus.Draft"/>.
    /// </exception>
    public void MarkAsPartiallyExtracted(string reason, Guid actorId, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Partial-extraction reason is required.", nameof(reason));
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("ActorId cannot be empty.", nameof(actorId));
        }

        if (Status != MechanicAnalysisStatus.Draft)
        {
            throw new InvalidMechanicAnalysisStateException(
                Id,
                Status,
                "mark as partially extracted",
                MechanicAnalysisStatus.Draft);
        }

        var previous = Status;
        Status = MechanicAnalysisStatus.PartiallyExtracted;
        ReviewedBy = actorId;
        ReviewedAt = utcNow;
        RejectionReason = reason.Trim();

        AddDomainEvent(new MechanicAnalysisStatusChangedEvent(Id, previous, Status, actorId, RejectionReason));
    }

    /// <summary>
    /// Approves a single claim. Valid only while the analysis is InReview.
    /// </summary>
    public void ApproveClaim(Guid claimId, Guid reviewerId, DateTime utcNow)
    {
        var claim = RequireClaimUnderReview(claimId, "approve claim");
        claim.Approve(reviewerId, utcNow);
    }

    /// <summary>
    /// Rejects a single claim with a note. Valid only while the analysis is InReview.
    /// </summary>
    public void RejectClaim(Guid claimId, Guid reviewerId, string note, DateTime utcNow)
    {
        var claim = RequireClaimUnderReview(claimId, "reject claim");
        claim.Reject(reviewerId, note, utcNow);
    }

    // === Suppression (T5 kill-switch) ===

    /// <summary>
    /// Applies a takedown (suppress) on the analysis. Allowed from any status — suppression is
    /// orthogonal to lifecycle.
    /// </summary>
    public void Suppress(
        Guid actorId,
        string reason,
        SuppressionRequestSource requestSource,
        DateTime? requestedAt,
        DateTime utcNow)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("ActorId cannot be empty.", nameof(actorId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Suppression reason is required (legal evidence chain).", nameof(reason));
        }

        if (IsSuppressed)
        {
            throw new InvalidOperationException($"MechanicAnalysis {Id} is already suppressed.");
        }

        IsSuppressed = true;
        SuppressedAt = utcNow;
        SuppressedBy = actorId;
        SuppressionReason = reason.Trim();
        SuppressionRequestSource = requestSource;
        SuppressionRequestedAt = requestedAt;

        AddDomainEvent(new MechanicAnalysisSuppressedEvent(
            Id,
            actorId,
            SuppressionReason,
            requestSource,
            requestedAt));
    }

    /// <summary>
    /// Lifts the takedown. Clears all suppression tracking fields.
    /// </summary>
    public void Unsuppress(Guid actorId, string reason, DateTime utcNow)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("ActorId cannot be empty.", nameof(actorId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Unsuppression reason is required.", nameof(reason));
        }

        if (!IsSuppressed)
        {
            throw new InvalidOperationException($"MechanicAnalysis {Id} is not suppressed.");
        }

        IsSuppressed = false;
        SuppressedAt = null;
        SuppressedBy = null;
        SuppressionReason = null;
        SuppressionRequestSource = null;
        SuppressionRequestedAt = null;

        AddDomainEvent(new MechanicAnalysisUnsuppressedEvent(Id, actorId, reason.Trim()));
    }

    // === T8 cost cap override ===

    /// <summary>
    /// Raises the cost cap with an admin justification. All three override fields are set
    /// atomically (DB CHECK enforces all-or-none).
    /// </summary>
    public void OverrideCostCap(Guid actorId, decimal newCapUsd, string reason, DateTime utcNow)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("ActorId cannot be empty.", nameof(actorId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Override reason is required.", nameof(reason));
        }

        if (newCapUsd <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newCapUsd),
                newCapUsd,
                "New cap must be strictly positive.");
        }

        if (newCapUsd <= CostCapUsd)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newCapUsd),
                newCapUsd,
                $"New cap must be greater than the current cap ({CostCapUsd:C}).");
        }

        var previous = CostCapUsd;
        CostCapUsd = newCapUsd;
        CostCapOverrideAt = utcNow;
        CostCapOverrideBy = actorId;
        CostCapOverrideReason = reason.Trim();

        AddDomainEvent(new MechanicAnalysisCostCapOverriddenEvent(
            Id,
            actorId,
            previous,
            newCapUsd,
            CostCapOverrideReason));
    }

    // === AI comprehension certification methods (ADR-051 M2) ===

    /// <summary>
    /// Syncs certification state from a freshly computed <see cref="MechanicAnalysisMetrics"/>
    /// snapshot. Should be called by the application layer after each metrics computation run.
    /// </summary>
    public void ApplyMetricsResult(MechanicAnalysisMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        if (metrics.MechanicAnalysisId != Id) throw new ArgumentException("Metrics do not belong to this analysis.", nameof(metrics));
        LastMetricsId = metrics.Id;
        CertificationStatus = metrics.CertificationStatus;
        CertifiedAt = metrics.CertificationStatus == CertificationStatus.Certified ? metrics.ComputedAt : null;
        CertifiedByUserId = null;
        CertificationOverrideReason = null;
    }

    /// <summary>
    /// Raises <see cref="MechanicAnalysisCertifiedEvent"/> for the automatic (non-override)
    /// certification path (ADR-051 M2). Must be called only after <see cref="ApplyMetricsResult"/>
    /// has synced <see cref="CertificationStatus"/> to <see cref="CertificationStatus.Certified"/>
    /// from a metrics computation that passed the configured thresholds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This overload exists because <see cref="AggregateRoot{TId}.AddDomainEvent"/> is
    /// <c>protected</c>: the application handler cannot raise the event directly, so the
    /// aggregate exposes a narrow method that encodes the automatic path's invariants
    /// (<c>WasOverride=false</c>, no override reason, no human actor) and delegates the
    /// event dispatch to the aggregate.
    /// </para>
    /// <para>
    /// <see cref="CertifiedByUserId"/> is <see cref="Guid.Empty"/> for the automatic path — there
    /// is no human actor. The override path (<see cref="CertifyViaOverride"/>) carries a real user id.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the aggregate is not currently <see cref="CertificationStatus.Certified"/> —
    /// the caller must apply metrics first and only invoke this method when the metrics produced
    /// a passing score.
    /// </exception>
    public void RaiseAutomaticCertificationEvent(DateTimeOffset certifiedAt)
    {
        if (CertificationStatus != CertificationStatus.Certified)
        {
            throw new InvalidOperationException(
                $"Cannot raise automatic certification event on MechanicAnalysis {Id}: " +
                $"current CertificationStatus is {CertificationStatus}, expected Certified.");
        }

        AddDomainEvent(new MechanicAnalysisCertifiedEvent(
            AnalysisId: Id,
            SharedGameId: SharedGameId,
            WasOverride: false,
            OverrideReason: null,
            CertifiedByUserId: Guid.Empty,
            CertifiedAt: certifiedAt));
    }

    /// <summary>
    /// Admin escalation path: certifies the analysis despite failing automated thresholds.
    /// Requires a justification of 20..500 characters and prior metrics to have been applied.
    /// </summary>
    public void CertifyViaOverride(string reason, Guid userId, DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length is < 20 or > 500)
            throw new ArgumentException("Reason must be 20..500 chars.", nameof(reason));
        if (LastMetricsId is null)
            throw new InvalidOperationException("Cannot override without prior metrics.");
        if (CertificationStatus == CertificationStatus.Certified)
            throw new InvalidOperationException("Already certified.");

        CertificationStatus = CertificationStatus.Certified;
        CertifiedAt = utcNow;
        CertifiedByUserId = userId;
        CertificationOverrideReason = reason;
    }

    /// <summary>
    /// Raises <see cref="MechanicAnalysisCertifiedEvent"/> for the admin-override certification
    /// path (ADR-051 Sprint 1 / Task 24). Must be called only after <see cref="CertifyViaOverride"/>
    /// has mutated <see cref="CertificationStatus"/> to <see cref="CertificationStatus.Certified"/>
    /// and populated <see cref="CertifiedByUserId"/> + <see cref="CertificationOverrideReason"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Symmetric with <see cref="RaiseAutomaticCertificationEvent"/>: the event is dispatched from
    /// the handler rather than inside <see cref="CertifyViaOverride"/> itself because the caller
    /// owns the <c>IUnitOfWork</c> boundary — state mutation and event raise share the same
    /// <c>SaveChangesAsync</c> commit, so either both happen or neither does.
    /// </para>
    /// <para>
    /// <see cref="AggregateRoot{TId}.AddDomainEvent"/> is <c>protected</c>, so the application
    /// handler cannot raise the event directly; this narrow method encodes the override path's
    /// invariants (<c>WasOverride=true</c>, reason + actor id carried forward) and delegates
    /// dispatch to the aggregate.
    /// </para>
    /// </remarks>
    /// <param name="certifiedAt">Timestamp stamped on the event; the handler passes the same value
    /// it used for <see cref="CertifyViaOverride"/> so the aggregate state and the event agree.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the aggregate is not currently <see cref="CertificationStatus.Certified"/> —
    /// the caller must invoke <see cref="CertifyViaOverride"/> first.
    /// </exception>
    public void RaiseOverrideCertificationEvent(DateTimeOffset certifiedAt)
    {
        if (CertificationStatus != CertificationStatus.Certified)
        {
            throw new InvalidOperationException(
                $"Cannot raise override certification event on MechanicAnalysis {Id}: " +
                $"current CertificationStatus is {CertificationStatus}, expected Certified.");
        }

        if (CertifiedByUserId is null)
        {
            throw new InvalidOperationException(
                $"Cannot raise override certification event on MechanicAnalysis {Id}: " +
                $"CertifiedByUserId is null. Override path requires a real actor id — " +
                $"call CertifyViaOverride(reason, userId, utcNow) first.");
        }

        AddDomainEvent(new MechanicAnalysisCertifiedEvent(
            AnalysisId: Id,
            SharedGameId: SharedGameId,
            WasOverride: true,
            OverrideReason: CertificationOverrideReason,
            CertifiedByUserId: CertifiedByUserId.Value,
            CertifiedAt: certifiedAt));
    }

    // === Helpers ===

    private void EnsureTransitionAllowed(string operation, MechanicAnalysisStatus target)
    {
        if (!ValidTransitions.TryGetValue(Status, out var allowedTargets) ||
            Array.IndexOf(allowedTargets, target) < 0)
        {
            var allowedFrom = ValidTransitions
                .Where(kv => kv.Value.Contains(target))
                .Select(kv => kv.Key)
                .ToArray();

            throw new InvalidMechanicAnalysisStateException(
                Id,
                Status,
                operation,
                allowedFrom);
        }
    }

    private MechanicClaim RequireClaimUnderReview(Guid claimId, string operation)
    {
        if (Status != MechanicAnalysisStatus.InReview)
        {
            throw new InvalidMechanicAnalysisStateException(
                Id,
                Status,
                operation,
                MechanicAnalysisStatus.InReview);
        }

        var claim = _claims.FirstOrDefault(c => c.Id == claimId)
            ?? throw new InvalidOperationException(
                $"Claim {claimId} does not belong to MechanicAnalysis {Id}.");

        return claim;
    }
}

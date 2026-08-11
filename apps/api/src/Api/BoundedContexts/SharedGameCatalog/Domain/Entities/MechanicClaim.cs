using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// A single rephrased rule extracted from a rulebook, attached to a parent
/// <see cref="Aggregates.MechanicAnalysis"/>. Carries its attribution citations and per-claim
/// review status.
/// </summary>
/// <remarks>
/// Lifecycle:
/// - Created as <see cref="MechanicClaimStatus.Pending"/> when the AI produces it.
/// - Moves to <see cref="MechanicClaimStatus.Approved"/> or <see cref="MechanicClaimStatus.Rejected"/>
///   via admin review during the InReview phase of the parent analysis.
/// - A parent analysis can only be promoted to <see cref="MechanicAnalysisStatus.Published"/>
///   when every claim is <see cref="MechanicClaimStatus.Approved"/> (AC-10).
///
/// Invariants:
/// - Must have at least one citation (ADR-051 T3).
/// - Rejection requires a <see cref="RejectionNote"/>.
/// </remarks>
public sealed class MechanicClaim : Entity<Guid>
{
    private readonly List<MechanicCitation> _citations = new();

    /// <summary>FK to the parent <see cref="Aggregates.MechanicAnalysis"/>.</summary>
    public Guid AnalysisId { get; private set; }

    /// <summary>Logical section of the rulebook this claim belongs to.</summary>
    public MechanicSection Section { get; private set; }

    /// <summary>Rephrased rule text (player-facing).</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>Display order inside the section (0-based).</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>Per-claim review status.</summary>
    public MechanicClaimStatus Status { get; private set; }

    /// <summary>Admin who last reviewed this claim (null until first review).</summary>
    public Guid? ReviewedBy { get; private set; }

    /// <summary>UTC timestamp of the last review action (null until first review).</summary>
    public DateTime? ReviewedAt { get; private set; }

    /// <summary>Reason the claim was rejected. Required when <see cref="Status"/> is Rejected.</summary>
    public string? RejectionNote { get; private set; }

    /// <summary>Optional note captured on approval (#526 AC-6). Distinct from <see cref="RejectionNote"/>.</summary>
    public string? ReviewNote { get; private set; }

    /// <summary>Attribution citations (minimum 1 — ADR-051 T3).</summary>
    public IReadOnlyList<MechanicCitation> Citations => _citations.AsReadOnly();

    /// <summary>
    /// Stable JSONPath anchor of this claim's RAW source object, captured by the parser before any
    /// drop/reorder/compaction (#2782 D4), e.g. "$.mechanics[2]" or "$.victory". Used to correlate a
    /// guardrail violation's Path to exactly one claim WITHIN the originating pipeline execution.
    /// NOT persisted or reloaded — it is empty on ALL reconstituted claims (there is no
    /// mechanic_claims column for it), so it is only meaningful during the run that parsed the claim.
    /// </summary>
    public string SourceAnchor { get; private set; } = string.Empty;

    private readonly List<MechanicClaimValidation> _validations = new();

    /// <summary>
    /// Per-rule guardrail outcomes correlated to this claim at pipeline time (#2782 D4), one per
    /// rule family (T1/T2/T3a/T3b/T4) evaluated for the owning section. Empty when the section had
    /// no captured <c>SectionOutcomes</c> (e.g. it succeeded before D3 landed) or on claims that
    /// have not yet gone through <see cref="AttachValidations"/>.
    /// </summary>
    public IReadOnlyList<MechanicClaimValidation> Validations => _validations.AsReadOnly();

    /// <summary>
    /// True when the claim was instantiated via <see cref="Create"/> and is not yet persisted;
    /// false when rehydrated from storage via <see cref="Reconstitute"/>.
    /// </summary>
    /// <remarks>
    /// Consumed by <see cref="Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories.MechanicAnalysisRepository.Update(Aggregates.MechanicAnalysis)"/>
    /// to disambiguate <c>EntityState.Added</c> vs <c>EntityState.Modified</c> when reattaching the
    /// aggregate graph. Without this flag, EF Core's <c>DbSet.Update</c> graph traversal marks every
    /// reachable child with a non-default <see cref="Guid"/> key as <c>Modified</c>, which causes
    /// UPDATEs against non-existent rows when new claims are appended to an aggregate loaded with
    /// <c>AsNoTracking</c> (e.g., the M1.2 executor pipeline).
    /// </remarks>
    public bool IsNew { get; private set; } = true;

    /// <summary>EF Core constructor. Do not use directly.</summary>
    private MechanicClaim() : base()
    {
    }

    private MechanicClaim(
        Guid id,
        Guid analysisId,
        MechanicSection section,
        string text,
        int displayOrder)
        : base(id)
    {
        AnalysisId = analysisId;
        Section = section;
        Text = text;
        DisplayOrder = displayOrder;
        Status = MechanicClaimStatus.Pending;
    }

    /// <summary>
    /// Factory that creates a new pending claim with its citations.
    /// </summary>
    /// <exception cref="ArgumentException">If <paramref name="text"/> is blank or citations is empty.</exception>
    public static MechanicClaim Create(
        Guid analysisId,
        MechanicSection section,
        string text,
        int displayOrder,
        IEnumerable<MechanicCitation> citations)
    {
        if (analysisId == Guid.Empty)
        {
            throw new ArgumentException("AnalysisId cannot be empty.", nameof(analysisId));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Claim text cannot be empty.", nameof(text));
        }

        if (displayOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displayOrder),
                displayOrder,
                "DisplayOrder must be non-negative.");
        }

        var citationList = citations?.ToList() ?? new List<MechanicCitation>();
        if (citationList.Count == 0)
        {
            throw new ArgumentException(
                "At least one citation is required (ADR-051 T3).",
                nameof(citations));
        }

        var claim = new MechanicClaim(
            id: Guid.NewGuid(),
            analysisId: analysisId,
            section: section,
            text: text.Trim(),
            displayOrder: displayOrder);

        claim._citations.AddRange(citationList);
        return claim;
    }

    /// <summary>
    /// Factory that creates a new pending claim with a pre-allocated <paramref name="id"/>.
    /// Used by the M1.2 pipeline parser, where citation FKs are wired before the claim entity
    /// exists, so the claim Id must be known up front. Preserves <see cref="IsNew"/> = <c>true</c>
    /// so the repository's reattachment logic emits INSERT (not UPDATE) for the new graph.
    /// </summary>
    /// <exception cref="ArgumentException">If <paramref name="id"/> or <paramref name="analysisId"/> is empty,
    /// <paramref name="text"/> is blank, or citations is empty.</exception>
    public static MechanicClaim CreateWithId(
        Guid id,
        Guid analysisId,
        MechanicSection section,
        string text,
        int displayOrder,
        IEnumerable<MechanicCitation> citations,
        string sourceAnchor)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        }

        if (analysisId == Guid.Empty)
        {
            throw new ArgumentException("AnalysisId cannot be empty.", nameof(analysisId));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Claim text cannot be empty.", nameof(text));
        }

        if (displayOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displayOrder),
                displayOrder,
                "DisplayOrder must be non-negative.");
        }

        var citationList = citations?.ToList() ?? new List<MechanicCitation>();
        if (citationList.Count == 0)
        {
            throw new ArgumentException(
                "At least one citation is required (ADR-051 T3).",
                nameof(citations));
        }

        var claim = new MechanicClaim(
            id: id,
            analysisId: analysisId,
            section: section,
            text: text.Trim(),
            displayOrder: displayOrder)
        {
            SourceAnchor = sourceAnchor
        };

        claim._citations.AddRange(citationList);
        return claim;
    }

    /// <summary>
    /// Rehydrates a claim from persistence. Used exclusively by the repository's
    /// <c>MapToDomain</c>; bypasses validation because invariants were enforced at creation time.
    /// </summary>
    public static MechanicClaim Reconstitute(
        Guid id,
        Guid analysisId,
        MechanicSection section,
        string text,
        int displayOrder,
        MechanicClaimStatus status,
        Guid? reviewedBy,
        DateTime? reviewedAt,
        string? rejectionNote,
        IEnumerable<MechanicCitation> citations,
        string? reviewNote = null,
        string? sourceAnchor = null,
        IEnumerable<MechanicClaimValidation>? validations = null)
    {
        ArgumentNullException.ThrowIfNull(citations);

        var claim = new MechanicClaim
        {
            Id = id,
            AnalysisId = analysisId,
            Section = section,
            Text = text,
            DisplayOrder = displayOrder,
            Status = status,
            ReviewedBy = reviewedBy,
            ReviewedAt = reviewedAt,
            RejectionNote = rejectionNote,
            ReviewNote = reviewNote,
            SourceAnchor = sourceAnchor ?? string.Empty,
            IsNew = false
        };

        claim._citations.AddRange(citations);

        if (validations is not null)
        {
            claim._validations.AddRange(validations);
        }

        return claim;
    }

    /// <summary>Attach the correlated per-rule guardrail outcomes captured at pipeline time (#2782 D4).</summary>
    internal void AttachValidations(IReadOnlyList<MechanicClaimValidation> validations)
    {
        ArgumentNullException.ThrowIfNull(validations);
        _validations.Clear();
        _validations.AddRange(validations);
    }

    /// <summary>
    /// Approves the claim, optionally capturing a review note (#526 AC-6). Idempotent:
    /// re-approving is a no-op except for refreshing the note.
    /// </summary>
    internal void Approve(Guid reviewerId, DateTime utcNow, string? note = null)
    {
        if (reviewerId == Guid.Empty)
        {
            throw new ArgumentException("ReviewerId cannot be empty.", nameof(reviewerId));
        }

        Status = MechanicClaimStatus.Approved;
        ReviewedBy = reviewerId;
        ReviewedAt = utcNow;
        RejectionNote = null;
        ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    /// <summary>
    /// Rejects the claim with a reason. The claim stays attached but cannot be published.
    /// </summary>
    internal void Reject(Guid reviewerId, string note, DateTime utcNow)
    {
        if (reviewerId == Guid.Empty)
        {
            throw new ArgumentException("ReviewerId cannot be empty.", nameof(reviewerId));
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("Rejection note is required when rejecting a claim.", nameof(note));
        }

        Status = MechanicClaimStatus.Rejected;
        ReviewedBy = reviewerId;
        ReviewedAt = utcNow;
        RejectionNote = note.Trim();
        ReviewNote = null;
    }

    /// <summary>
    /// Moves the claim back to pending (used when the parent analysis transitions Rejected→InReview).
    /// </summary>
    internal void ResetToPending()
    {
        Status = MechanicClaimStatus.Pending;
        ReviewedBy = null;
        ReviewedAt = null;
        RejectionNote = null;
        ReviewNote = null;
    }
}

using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
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

    /// <summary>Attribution citations (minimum 1 — ADR-051 T3).</summary>
    public IReadOnlyList<MechanicCitation> Citations => _citations.AsReadOnly();

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
        IEnumerable<MechanicCitation> citations)
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
            IsNew = false
        };

        claim._citations.AddRange(citations);
        return claim;
    }

    /// <summary>
    /// Approves the claim. Idempotent: re-approving is a no-op.
    /// </summary>
    internal void Approve(Guid reviewerId, DateTime utcNow)
    {
        if (reviewerId == Guid.Empty)
        {
            throw new ArgumentException("ReviewerId cannot be empty.", nameof(reviewerId));
        }

        Status = MechanicClaimStatus.Approved;
        ReviewedBy = reviewerId;
        ReviewedAt = utcNow;
        RejectionNote = null;
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
    }
}

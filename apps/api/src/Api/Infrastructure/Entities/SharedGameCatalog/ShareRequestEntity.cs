namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// EF Core entity for ShareRequest aggregate.
/// Maps to the share_requests table.
/// </summary>
public class ShareRequestEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SourceGameId { get; set; }
    public Guid? TargetSharedGameId { get; set; }

    /// <summary>
    /// ID of the private game being proposed (for NewGameProposal contributions).
    /// Issue #3665: Added for Phase 4 - Proposal System.
    /// </summary>
    public Guid? SourcePrivateGameId { get; set; }

    /// <summary>
    /// Status enum: 0=Pending, 1=InReview, 2=ChangesRequested, 3=Approved, 4=Rejected, 5=Withdrawn
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Status before review started (for returning after release).
    /// </summary>
    public int? StatusBeforeReview { get; set; }

    /// <summary>
    /// Contribution type: 0=NewGame, 1=AdditionalContent
    /// </summary>
    public int ContributionType { get; set; }

    public string? UserNotes { get; set; }
    public string? AdminFeedback { get; set; }

    public Guid? ReviewingAdminId { get; set; }
    public DateTime? ReviewStartedAt { get; set; }
    public DateTime? ReviewLockExpiresAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }

    // #3651 — token di concorrenza sulla colonna di sistema `xmin` (ADR-060). Era una `bytea`
    // che nulla popolava da quando #2305 ha rimosso il trigger: restava NULL, EF confrontava
    // NULL = NULL e due admin potevano risolvere la stessa richiesta senza accorgersene.
    public uint Xmin { get; set; }

    /// <summary>
    /// R2 object key of the pending cover image materialized from a PDF page
    /// (for CoverChange contributions). Task 4: Game Cover-da-PDF.
    /// </summary>
    public string? PendingCoverR2Key { get; set; }

    /// <summary>
    /// Zero-based index of the PDF page the pending cover was rendered from
    /// (for CoverChange contributions). Task 4: Game Cover-da-PDF.
    /// </summary>
    public int? CoverPageIndex { get; set; }

    /// <summary>
    /// ID of the source PDF document the pending cover was rendered from
    /// (for CoverChange contributions). Task 4: Game Cover-da-PDF.
    /// </summary>
    public Guid? SourcePdfDocumentId { get; set; }

    // Navigation properties
    public SharedGameEntity SourceGame { get; set; } = default!;
    public SharedGameEntity? TargetSharedGame { get; set; }

    /// <summary>
    /// Navigation property to the private game (for NewGameProposal contributions).
    /// Issue #3665: Added for Phase 4 - Proposal System.
    /// </summary>
    public UserLibrary.PrivateGameEntity? PrivateGame { get; set; }

    public ICollection<ShareRequestDocumentEntity> AttachedDocuments { get; set; } = new List<ShareRequestDocumentEntity>();
}

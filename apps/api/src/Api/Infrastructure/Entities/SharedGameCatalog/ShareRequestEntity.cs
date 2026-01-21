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

    public byte[]? RowVersion { get; set; }

    // Navigation properties
    public SharedGameEntity SourceGame { get; set; } = default!;
    public SharedGameEntity? TargetSharedGame { get; set; }
    public ICollection<ShareRequestDocumentEntity> AttachedDocuments { get; set; } = new List<ShareRequestDocumentEntity>();
}

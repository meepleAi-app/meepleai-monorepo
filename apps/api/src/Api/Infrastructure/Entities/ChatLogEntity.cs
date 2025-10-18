namespace Api.Infrastructure.Entities;

public class ChatLogEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChatId { get; set; } = Guid.Empty;

    /// <summary>
    /// User who created the message. NULL for AI-generated messages.
    /// </summary>
    public string? UserId { get; set; }

    public string Level { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string? MetadataJson { get; set; } = null;

    /// <summary>
    /// Message ordering within chat (0-indexed). Used for invalidation logic.
    /// </summary>
    public int SequenceNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp of last edit. NULL if never edited.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Soft delete flag. Deleted messages hidden from UI but retained for audit.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Timestamp when message was soft-deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// User who deleted the message (may differ from creator for admin deletions).
    /// </summary>
    public string? DeletedByUserId { get; set; }

    /// <summary>
    /// Flag indicating message is invalidated due to prior message edit/delete.
    /// </summary>
    public bool IsInvalidated { get; set; } = false;

    // Navigation properties
    public ChatEntity Chat { get; set; } = default!;
    public UserEntity? User { get; set; }
    public UserEntity? DeletedByUser { get; set; }
}

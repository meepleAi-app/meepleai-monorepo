using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.UserNotifications;

/// <summary>
/// EF Core entity for user notifications.
/// Persistence model for Notification aggregate.
/// </summary>
[Table("notifications")]
internal class NotificationEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// User who receives this notification.
    /// </summary>
    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    /// <summary>
    /// Notification type (event category).
    /// Examples: pdf_upload_completed, rule_spec_generated, processing_failed.
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Severity level for UI presentation.
    /// Values: info, success, warning, error.
    /// </summary>
    [Required]
    [MaxLength(20)]
    [Column("severity")]
    public string Severity { get; set; } = string.Empty;

    /// <summary>
    /// Brief notification title.
    /// </summary>
    [Required]
    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed notification message.
    /// </summary>
    [Required]
    [Column("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional deep-link target (e.g., /chat/thread-id, /pdf/doc-id).
    /// </summary>
    [MaxLength(500)]
    [Column("link")]
    public string? Link { get; set; }

    /// <summary>
    /// Optional JSON metadata for additional context.
    /// Stored as JSONB for efficient querying.
    /// </summary>
    [Column("metadata", TypeName = "jsonb")]
    public string? Metadata { get; set; }

    /// <summary>
    /// Whether notification has been read by user.
    /// </summary>
    [Required]
    [Column("is_read")]
    public bool IsRead { get; set; } = false;

    /// <summary>
    /// Timestamp when notification was created.
    /// </summary>
    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when notification was read (null if unread).
    /// </summary>
    [Column("read_at")]
    public DateTime? ReadAt { get; set; }
}

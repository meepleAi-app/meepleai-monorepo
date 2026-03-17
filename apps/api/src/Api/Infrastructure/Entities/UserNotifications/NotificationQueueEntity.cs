using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.UserNotifications;

/// <summary>
/// EF Core entity for notification queue items.
/// Persistence model for NotificationQueueItem aggregate.
/// Supports multi-channel delivery (email, Slack DM, Slack team) with retry policy.
/// </summary>
[Table("notification_queue_items")]
public class NotificationQueueEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(20)]
    [Column("channel_type")]
    public string ChannelType { get; set; } = string.Empty;

    [Column("recipient_user_id")]
    public Guid? RecipientUserId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("notification_type")]
    public string NotificationType { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized INotificationPayload with polymorphic $type discriminator.
    /// Stored as JSONB for efficient querying.
    /// </summary>
    [Required]
    [Column("payload", TypeName = "jsonb")]
    public string Payload { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("slack_channel_target")]
    public string? SlackChannelTarget { get; set; }

    [MaxLength(20)]
    [Column("slack_team_id")]
    public string? SlackTeamId { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "pending";

    [Required]
    [Column("retry_count")]
    public int RetryCount { get; set; }

    [Required]
    [Column("max_retries")]
    public int MaxRetries { get; set; } = 3;

    [Column("next_retry_at")]
    public DateTime? NextRetryAt { get; set; }

    [MaxLength(2000)]
    [Column("last_error")]
    public string? LastError { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    [Required]
    [Column("correlation_id")]
    public Guid CorrelationId { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.UserNotifications;

/// <summary>
/// EF Core entity for email queue items.
/// Persistence model for EmailQueueItem aggregate.
/// Issue #4417: Email notification queue with retry policy.
/// </summary>
[Table("email_queue_items")]
public class EmailQueueEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(320)]
    [Column("to_address")]
    public string To { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    [Column("subject")]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [Column("html_body")]
    public string HtmlBody { get; set; } = string.Empty;

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
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    [Column("failed_at")]
    public DateTime? FailedAt { get; set; }

    /// <summary>
    /// Optional correlation ID for cross-channel notification tracking.
    /// </summary>
    [Column("correlation_id")]
    public Guid? CorrelationId { get; set; }
}

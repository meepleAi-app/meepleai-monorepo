using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities;

/// <summary>
/// Entity representing system alerts from Prometheus AlertManager.
/// OPS-07: Alerting system for critical errors and anomalies.
/// </summary>
[Table("alerts")]
public class AlertEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Type of alert (e.g., "HighErrorRate", "DatabaseDown", "QdrantDown").
    /// </summary>
    [Required]
    [MaxLength(100)]
    [Column("alert_type")]
    public string AlertType { get; set; } = string.Empty;

    /// <summary>
    /// Severity level: "critical", "warning", "info".
    /// </summary>
    [Required]
    [MaxLength(20)]
    [Column("severity")]
    public string Severity { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable alert message.
    /// </summary>
    [Required]
    [Column("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata from Prometheus (labels, annotations, etc.).
    /// Stored as JSON.
    /// </summary>
    [Column("metadata", TypeName = "jsonb")]
    public string? Metadata { get; set; }

    /// <summary>
    /// Timestamp when the alert was first triggered.
    /// </summary>
    [Required]
    [Column("triggered_at")]
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the alert was resolved (null if still active).
    /// </summary>
    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Whether the alert is currently active.
    /// </summary>
    [Required]
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// JSON object tracking which channels successfully received the alert.
    /// Example: {"email": true, "slack": true, "pagerduty": false}
    /// </summary>
    [Column("channel_sent", TypeName = "jsonb")]
    public string? ChannelSent { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.Administration;

/// <summary>
/// EF Core entity for AI insight feedback persistence.
/// Issue #4124: AI Insights Runtime Validation (Performance + Accuracy).
/// </summary>
[Table("insight_feedback")]
public class InsightFeedbackEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    /// <summary>
    /// The insight ID as returned from the dashboard insights endpoint.
    /// Format: "{type}-{userId}-{date}" or "{type}-{entityId}".
    /// </summary>
    [Required]
    [Column("insight_id")]
    [MaxLength(200)]
    public string InsightId { get; set; } = string.Empty;

    /// <summary>
    /// Insight type: Backlog, RulesReminder, Recommendation, Streak, Achievement.
    /// </summary>
    [Required]
    [Column("insight_type")]
    [MaxLength(30)]
    public string InsightType { get; set; } = string.Empty;

    /// <summary>
    /// Whether the user found the insight relevant/useful.
    /// </summary>
    [Required]
    [Column("is_relevant")]
    public bool IsRelevant { get; set; }

    /// <summary>
    /// Optional user comment explaining their feedback.
    /// </summary>
    [Column("comment")]
    [MaxLength(500)]
    public string? Comment { get; set; }

    [Required]
    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }
}

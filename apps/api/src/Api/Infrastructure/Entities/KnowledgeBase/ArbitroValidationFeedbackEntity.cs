using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// EF Core entity for arbitro validation feedback persistence.
/// Issue #4328: Arbitro Agent Beta Testing and User Feedback Iteration.
/// </summary>
[Table("arbitro_validation_feedback")]
public class ArbitroValidationFeedbackEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("validation_id")]
    public Guid ValidationId { get; set; }

    [Required]
    [Column("game_session_id")]
    public Guid GameSessionId { get; set; }

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [Column("rating")]
    public int Rating { get; set; }

    [Required]
    [Column("accuracy")]
    [MaxLength(20)]
    public string Accuracy { get; set; } = string.Empty;

    [Column("comment")]
    [MaxLength(2000)]
    public string? Comment { get; set; }

    [Required]
    [Column("ai_decision")]
    [MaxLength(20)]
    public string AiDecision { get; set; } = string.Empty;

    [Required]
    [Column("ai_confidence")]
    public double AiConfidence { get; set; }

    [Required]
    [Column("had_conflicts")]
    public bool HadConflicts { get; set; }

    [Required]
    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }

    // Navigation properties
    public GameSessionEntity? GameSession { get; set; }
    public UserEntity? User { get; set; }
}

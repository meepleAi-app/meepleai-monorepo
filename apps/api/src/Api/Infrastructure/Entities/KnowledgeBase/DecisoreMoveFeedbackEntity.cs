using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// EF Core entity for Decisore move feedback persistence.
/// Issue #4335: Decisore Agent Beta Testing and User Feedback Iteration.
/// </summary>
[Table("decisore_move_feedback")]
public class DecisoreMoveFeedbackEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("suggestion_id")]
    public Guid SuggestionId { get; set; }

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
    [Column("quality")]
    [MaxLength(20)]
    public string Quality { get; set; } = string.Empty;

    [Column("comment")]
    [MaxLength(2000)]
    public string? Comment { get; set; }

    [Required]
    [Column("outcome")]
    [MaxLength(20)]
    public string Outcome { get; set; } = string.Empty;

    [Required]
    [Column("suggestion_followed")]
    public bool SuggestionFollowed { get; set; }

    [Required]
    [Column("top_suggested_move")]
    [MaxLength(10)]
    public string TopSuggestedMove { get; set; } = string.Empty;

    [Required]
    [Column("position_strength")]
    public double PositionStrength { get; set; }

    [Required]
    [Column("analysis_depth")]
    [MaxLength(20)]
    public string AnalysisDepth { get; set; } = string.Empty;

    [Required]
    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }

    // Navigation properties
    public GameSessionEntity? GameSession { get; set; }
    public UserEntity? User { get; set; }
}

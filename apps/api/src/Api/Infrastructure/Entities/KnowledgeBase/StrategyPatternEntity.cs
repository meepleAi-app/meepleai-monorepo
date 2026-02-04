using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// EF Core entity for strategy pattern persistence.
/// Issue #3493: PostgreSQL Schema Extensions for Multi-Agent System.
/// </summary>
[Table("strategy_patterns")]
public class StrategyPatternEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("game_id")]
    public Guid GameId { get; set; }

    [Required]
    [Column("pattern_name")]
    [MaxLength(200)]
    public string PatternName { get; set; } = string.Empty;

    [Column("applicable_phase")]
    [MaxLength(100)]
    public string? ApplicablePhase { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("evaluation_score")]
    public double? EvaluationScore { get; set; }

    [Column("board_conditions_json", TypeName = "jsonb")]
    public string? BoardConditionsJson { get; set; }

    [Column("move_sequence_json", TypeName = "jsonb")]
    public string? MoveSequenceJson { get; set; }

    [Column("source")]
    [MaxLength(100)]
    public string? Source { get; set; }

    [Column("embedding", TypeName = "vector(1536)")]
    public float[]? Embedding { get; set; }
}

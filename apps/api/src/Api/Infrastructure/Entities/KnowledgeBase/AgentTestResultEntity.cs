using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// EF Core entity for agent test result persistence.
/// Issue #3379: Agent Test Results History &amp; Persistence.
/// </summary>
[Table("agent_test_results")]
public class AgentTestResultEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("typology_id")]
    public Guid TypologyId { get; set; }

    [Column("strategy_override")]
    [MaxLength(50)]
    public string? StrategyOverride { get; set; }

    [Required]
    [Column("model_used")]
    [MaxLength(100)]
    public string ModelUsed { get; set; } = string.Empty;

    [Required]
    [Column("query")]
    public string Query { get; set; } = string.Empty;

    [Required]
    [Column("response")]
    public string Response { get; set; } = string.Empty;

    [Required]
    [Column("confidence_score")]
    public double ConfidenceScore { get; set; }

    [Required]
    [Column("tokens_used")]
    public int TokensUsed { get; set; }

    [Required]
    [Column("cost_estimate")]
    public decimal CostEstimate { get; set; }

    [Required]
    [Column("latency_ms")]
    public int LatencyMs { get; set; }

    [Column("citations_json", TypeName = "jsonb")]
    public string? CitationsJson { get; set; }

    [Required]
    [Column("executed_at")]
    public DateTime ExecutedAt { get; set; }

    [Required]
    [Column("executed_by")]
    public Guid ExecutedBy { get; set; }

    [Column("notes")]
    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Required]
    [Column("is_saved")]
    public bool IsSaved { get; set; }

    // Navigation properties
    public AgentTypologyEntity? Typology { get; set; }
    public UserEntity? User { get; set; }
}

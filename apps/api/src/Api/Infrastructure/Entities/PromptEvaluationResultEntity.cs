using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MeepleAI.Api.Infrastructure.Entities;

/// <summary>
/// Entity for storing prompt evaluation results
/// ADMIN-01 Phase 4: Prompt Testing Framework
/// Table: prompt_evaluation_results
/// </summary>
[Table("prompt_evaluation_results")]
public class PromptEvaluationResultEntity
{
    /// <summary>Unique evaluation run ID (Primary Key)</summary>
    [Key]
    [Column("id")]
    [MaxLength(100)]
    public required string Id { get; set; }

    /// <summary>Template ID that was evaluated</summary>
    [Column("template_id")]
    [MaxLength(100)]
    [Required]
    public required string TemplateId { get; set; }

    /// <summary>Version ID that was evaluated</summary>
    [Column("version_id")]
    [MaxLength(100)]
    [Required]
    public required string VersionId { get; set; }

    /// <summary>Dataset ID used for evaluation</summary>
    [Column("dataset_id")]
    [MaxLength(100)]
    [Required]
    public required string DatasetId { get; set; }

    /// <summary>Evaluation execution timestamp (UTC)</summary>
    [Column("executed_at")]
    [Required]
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Total queries executed in the evaluation</summary>
    [Column("total_queries")]
    [Required]
    public int TotalQueries { get; set; }

    /// <summary>Accuracy metric (0.0-1.0)</summary>
    [Column("accuracy")]
    [Required]
    public double Accuracy { get; set; }

    /// <summary>Hallucination rate metric (0.0-1.0)</summary>
    [Column("hallucination_rate")]
    [Required]
    public double HallucinationRate { get; set; }

    /// <summary>Average confidence metric (0.0-1.0)</summary>
    [Column("avg_confidence")]
    [Required]
    public double AvgConfidence { get; set; }

    /// <summary>Citation correctness metric (0.0-1.0)</summary>
    [Column("citation_correctness")]
    [Required]
    public double CitationCorrectness { get; set; }

    /// <summary>Average latency in milliseconds</summary>
    [Column("avg_latency_ms")]
    [Required]
    public double AvgLatencyMs { get; set; }

    /// <summary>Overall pass/fail status</summary>
    [Column("passed")]
    [Required]
    public bool Passed { get; set; }

    /// <summary>Summary message</summary>
    [Column("summary")]
    [MaxLength(500)]
    public string? Summary { get; set; }

    /// <summary>Detailed query results stored as JSON</summary>
    [Column("query_results_json", TypeName = "jsonb")]
    public string? QueryResultsJson { get; set; }

    /// <summary>Created at timestamp</summary>
    [Column("created_at")]
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Updated at timestamp</summary>
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

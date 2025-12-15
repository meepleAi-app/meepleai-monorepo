using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities;

/// <summary>
/// Entity for storing prompt evaluation results
/// ADMIN-01 Phase 4: Prompt Testing Framework
/// Table: prompt_evaluation_results
/// </summary>
[Table("prompt_evaluation_results")]
internal class PromptEvaluationResultEntity
{
    /// <summary>Unique evaluation run ID (Primary Key)</summary>
    // DDD-PHASE2: Converted to Guid for domain alignment
    [Key]
    [Column("id")]
    public required Guid Id { get; set; }

    /// <summary>Template ID that was evaluated</summary>
    // DDD-PHASE2: Converted to Guid for domain alignment
    [Column("template_id")]
    [Required]
    public required Guid TemplateId { get; set; }

    /// <summary>Version ID that was evaluated</summary>
    // DDD-PHASE2: Converted to Guid for domain alignment
    [Column("version_id")]
    [Required]
    public required Guid VersionId { get; set; }

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

    /// <summary>Accuracy metric: correctness of information (0.0-100.0)</summary>
    /// BGAI-041: Extended 5-metric quality evaluation framework
    [Column("accuracy")]
    [Required]
    public double Accuracy { get; set; }

    /// <summary>Relevance metric: appropriateness to context (0.0-100.0)</summary>
    /// BGAI-041: Replaces HallucinationRate
    [Column("relevance")]
    [Required]
    public double Relevance { get; set; }

    /// <summary>Completeness metric: thoroughness of coverage (0.0-100.0)</summary>
    /// BGAI-041: Replaces AvgConfidence
    [Column("completeness")]
    [Required]
    public double Completeness { get; set; }

    /// <summary>Clarity metric: understandability of output (0.0-100.0)</summary>
    /// BGAI-041: Replaces AvgLatencyMs
    [Column("clarity")]
    [Required]
    public double Clarity { get; set; }

    /// <summary>Citation Quality metric: reliability of source attribution (0.0-100.0)</summary>
    /// BGAI-041: Renamed from CitationCorrectness
    [Column("citation_quality")]
    [Required]
    public double CitationQuality { get; set; }

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

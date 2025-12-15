using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities;

/// <summary>
/// Entity for storing validation accuracy baseline measurements.
/// BGAI-039: Tracks how accurately the validation system identifies correct vs. incorrect responses.
/// Table: validation_accuracy_baselines
/// </summary>
[Table("validation_accuracy_baselines")]
internal class ValidationAccuracyBaselineEntity
{
    /// <summary>Unique baseline measurement ID (Primary Key)</summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>Context/description of what is being measured (e.g., "Overall Validation", "Layer 1: Confidence")</summary>
    [Column("context")]
    [MaxLength(200)]
    [Required]
    public required string Context { get; set; }

    /// <summary>Dataset ID used for baseline measurement</summary>
    [Column("dataset_id")]
    [MaxLength(100)]
    [Required]
    public required string DatasetId { get; set; }

    /// <summary>Evaluation result ID (foreign key to prompt_evaluation_results)</summary>
    [Column("evaluation_id")]
    public Guid? EvaluationId { get; set; }

    /// <summary>Measurement timestamp (UTC)</summary>
    [Column("measured_at")]
    [Required]
    public DateTime MeasuredAt { get; set; } = DateTime.UtcNow;

    /// <summary>True Positives: Valid responses correctly identified as valid</summary>
    [Column("true_positives")]
    [Required]
    public int TruePositives { get; set; }

    /// <summary>True Negatives: Invalid responses correctly identified as invalid</summary>
    [Column("true_negatives")]
    [Required]
    public int TrueNegatives { get; set; }

    /// <summary>False Positives: Invalid responses incorrectly identified as valid</summary>
    [Column("false_positives")]
    [Required]
    public int FalsePositives { get; set; }

    /// <summary>False Negatives: Valid responses incorrectly identified as invalid</summary>
    [Column("false_negatives")]
    [Required]
    public int FalseNegatives { get; set; }

    /// <summary>Total number of test cases</summary>
    [Column("total_cases")]
    [Required]
    public int TotalCases { get; set; }

    /// <summary>Calculated precision (0.0-1.0)</summary>
    [Column("precision")]
    [Required]
    public double Precision { get; set; }

    /// <summary>Calculated recall (0.0-1.0)</summary>
    [Column("recall")]
    [Required]
    public double Recall { get; set; }

    /// <summary>Calculated F1-score (0.0-1.0)</summary>
    [Column("f1_score")]
    [Required]
    public double F1Score { get; set; }

    /// <summary>Calculated accuracy (0.0-1.0)</summary>
    [Column("accuracy")]
    [Required]
    public double Accuracy { get; set; }

    /// <summary>Calculated specificity (0.0-1.0)</summary>
    [Column("specificity")]
    [Required]
    public double Specificity { get; set; }

    /// <summary>Matthews Correlation Coefficient (-1.0 to 1.0)</summary>
    [Column("matthews_correlation")]
    [Required]
    public double MatthewsCorrelation { get; set; }

    /// <summary>Whether accuracy meets baseline threshold (>= 0.80)</summary>
    [Column("meets_baseline")]
    [Required]
    public bool MeetsBaseline { get; set; }

    /// <summary>Quality level classification (0-5: Critical, Poor, Fair, Good, VeryGood, Excellent)</summary>
    [Column("quality_level")]
    [Required]
    public int QualityLevel { get; set; }

    /// <summary>Summary message</summary>
    [Column("summary")]
    [MaxLength(500)]
    public string? Summary { get; set; }

    /// <summary>Recommendations JSON (stored as JSONB)</summary>
    [Column("recommendations_json", TypeName = "jsonb")]
    public string? RecommendationsJson { get; set; }

    /// <summary>Created at timestamp</summary>
    [Column("created_at")]
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Updated at timestamp</summary>
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

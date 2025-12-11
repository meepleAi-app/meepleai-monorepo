using System.Text.Json.Serialization;

#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.Models;

/// <summary>
/// DTO for validation accuracy baseline measurement.
/// BGAI-039: Tracks how accurately the validation system identifies correct vs. incorrect responses.
/// </summary>
public class ValidationAccuracyBaselineDto
{
    /// <summary>Unique baseline measurement ID</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>Context/description of what is being measured</summary>
    [JsonPropertyName("context")]
    public required string Context { get; set; }

    /// <summary>Dataset ID used for baseline measurement</summary>
    [JsonPropertyName("dataset_id")]
    public required string DatasetId { get; set; }

    /// <summary>Evaluation result ID (if based on evaluation)</summary>
    [JsonPropertyName("evaluation_id")]
    public Guid? EvaluationId { get; set; }

    /// <summary>Measurement timestamp</summary>
    [JsonPropertyName("measured_at")]
    public DateTime MeasuredAt { get; set; }

    /// <summary>True Positives: Valid responses correctly identified as valid</summary>
    [JsonPropertyName("true_positives")]
    public int TruePositives { get; set; }

    /// <summary>True Negatives: Invalid responses correctly identified as invalid</summary>
    [JsonPropertyName("true_negatives")]
    public int TrueNegatives { get; set; }

    /// <summary>False Positives: Invalid responses incorrectly identified as valid</summary>
    [JsonPropertyName("false_positives")]
    public int FalsePositives { get; set; }

    /// <summary>False Negatives: Valid responses incorrectly identified as invalid</summary>
    [JsonPropertyName("false_negatives")]
    public int FalseNegatives { get; set; }

    /// <summary>Total number of test cases</summary>
    [JsonPropertyName("total_cases")]
    public int TotalCases { get; set; }

    /// <summary>Precision (0.0-1.0)</summary>
    [JsonPropertyName("precision")]
    public double Precision { get; set; }

    /// <summary>Recall (0.0-1.0)</summary>
    [JsonPropertyName("recall")]
    public double Recall { get; set; }

    /// <summary>F1-Score (0.0-1.0)</summary>
    [JsonPropertyName("f1_score")]
    public double F1Score { get; set; }

    /// <summary>Accuracy (0.0-1.0) - Target: >= 0.80</summary>
    [JsonPropertyName("accuracy")]
    public double Accuracy { get; set; }

    /// <summary>Specificity (0.0-1.0)</summary>
    [JsonPropertyName("specificity")]
    public double Specificity { get; set; }

    /// <summary>Matthews Correlation Coefficient (-1.0 to 1.0)</summary>
    [JsonPropertyName("matthews_correlation")]
    public double MatthewsCorrelation { get; set; }

    /// <summary>Whether accuracy meets baseline threshold (>= 0.80)</summary>
    [JsonPropertyName("meets_baseline")]
    public bool MeetsBaseline { get; set; }

    /// <summary>Quality level classification</summary>
    [JsonPropertyName("quality_level")]
    public required string QualityLevel { get; set; }

    /// <summary>Summary message</summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    /// <summary>Actionable recommendations</summary>
    [JsonPropertyName("recommendations")]
    public required List<string> Recommendations { get; set; }
}

/// <summary>
/// Request to measure validation accuracy baseline.
/// </summary>
public class MeasureValidationAccuracyRequest
{
    /// <summary>Context/description of what is being measured</summary>
    [JsonPropertyName("context")]
    public required string Context { get; set; }

    /// <summary>Dataset ID used for baseline measurement</summary>
    [JsonPropertyName("dataset_id")]
    public required string DatasetId { get; set; }

    /// <summary>Evaluation result ID to calculate accuracy from</summary>
    [JsonPropertyName("evaluation_id")]
    public required Guid EvaluationId { get; set; }

    /// <summary>Expected number of valid responses (ground truth)</summary>
    [JsonPropertyName("expected_valid_count")]
    public required int ExpectedValidCount { get; set; }

    /// <summary>Whether to store the baseline measurement in database</summary>
    [JsonPropertyName("store_baseline")]
    public bool StoreBaseline { get; set; } = true;
}

using System.Text.Json.Serialization;

namespace Api.Models;

/// <summary>
/// Test dataset for prompt evaluation
/// ADMIN-01 Phase 4: Prompt Testing Framework
/// </summary>
public class PromptTestDataset
{
    /// <summary>Unique dataset identifier</summary>
    [JsonPropertyName("dataset_id")]
    public required string DatasetId { get; set; }

    /// <summary>Template name this dataset is for (e.g., "qa-system-prompt")</summary>
    [JsonPropertyName("template_name")]
    public required string TemplateName { get; set; }

    /// <summary>Dataset version for tracking changes</summary>
    [JsonPropertyName("version")]
    public required string Version { get; set; }

    /// <summary>Dataset description and purpose</summary>
    [JsonPropertyName("description")]
    public required string Description { get; set; }

    /// <summary>Test cases in this dataset</summary>
    [JsonPropertyName("test_cases")]
    public required List<PromptTestCase> TestCases { get; set; }

    /// <summary>Quality thresholds for pass/fail</summary>
    [JsonPropertyName("quality_thresholds")]
    public required QualityThresholds Thresholds { get; set; }
}

/// <summary>
/// Individual test case within a dataset
/// </summary>
public class PromptTestCase
{
    /// <summary>Unique test case ID</summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>Test case category (e.g., "setup", "gameplay", "edge-case")</summary>
    [JsonPropertyName("category")]
    public required string Category { get; set; }

    /// <summary>Difficulty level (easy, medium, hard)</summary>
    [JsonPropertyName("difficulty")]
    public required string Difficulty { get; set; }

    /// <summary>User query to send to the system</summary>
    [JsonPropertyName("query")]
    public required string Query { get; set; }

    /// <summary>Game ID context for the query</summary>
    [JsonPropertyName("game_id")]
    public string? GameId { get; set; }

    /// <summary>Expected answer or key points (for accuracy checking)</summary>
    [JsonPropertyName("expected_answer")]
    public string? ExpectedAnswer { get; set; }

    /// <summary>Keywords that MUST appear in response</summary>
    [JsonPropertyName("required_keywords")]
    public List<string>? RequiredKeywords { get; set; }

    /// <summary>Keywords that MUST NOT appear (hallucination detection)</summary>
    [JsonPropertyName("forbidden_keywords")]
    public List<string>? ForbiddenKeywords { get; set; }

    /// <summary>Expected citations (document IDs or page numbers)</summary>
    [JsonPropertyName("expected_citations")]
    public List<string>? ExpectedCitations { get; set; }

    /// <summary>Minimum confidence threshold for this specific test case</summary>
    [JsonPropertyName("min_confidence")]
    public double? MinConfidence { get; set; }

    /// <summary>Maximum acceptable latency in milliseconds</summary>
    [JsonPropertyName("max_latency_ms")]
    public int? MaxLatencyMs { get; set; }
}

/// <summary>
/// Quality thresholds for evaluation pass/fail
/// </summary>
public class QualityThresholds
{
    /// <summary>Minimum accuracy (0.0-1.0), default 0.80</summary>
    [JsonPropertyName("min_accuracy")]
    public double MinAccuracy { get; set; } = 0.80;

    /// <summary>Maximum hallucination rate (0.0-1.0), default 0.10</summary>
    [JsonPropertyName("max_hallucination_rate")]
    public double MaxHallucinationRate { get; set; } = 0.10;

    /// <summary>Minimum average confidence (0.0-1.0), default 0.70</summary>
    [JsonPropertyName("min_avg_confidence")]
    public double MinAvgConfidence { get; set; } = 0.70;

    /// <summary>Minimum citation correctness (0.0-1.0), default 0.80</summary>
    [JsonPropertyName("min_citation_correctness")]
    public double MinCitationCorrectness { get; set; } = 0.80;

    /// <summary>Maximum average latency in milliseconds, default 3000</summary>
    [JsonPropertyName("max_avg_latency_ms")]
    public int MaxAvgLatencyMs { get; set; } = 3000;
}

/// <summary>
/// Result of evaluating a prompt version
/// </summary>
public class PromptEvaluationResult
{
    /// <summary>Unique evaluation run ID</summary>
    public required string EvaluationId { get; set; }

    /// <summary>Template ID evaluated</summary>
    public required string TemplateId { get; set; }

    /// <summary>Version ID evaluated</summary>
    public required string VersionId { get; set; }

    /// <summary>Dataset used for evaluation</summary>
    public required string DatasetId { get; set; }

    /// <summary>Evaluation execution timestamp</summary>
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Total queries executed</summary>
    public int TotalQueries { get; set; }

    /// <summary>Calculated metrics</summary>
    public required EvaluationMetrics Metrics { get; set; }

    /// <summary>Overall pass/fail status</summary>
    public bool Passed { get; set; }

    /// <summary>Individual query results for detailed analysis</summary>
    public required List<QueryEvaluationResult> QueryResults { get; set; }

    /// <summary>Summary message</summary>
    public string? Summary { get; set; }
}

/// <summary>
/// Evaluation metrics (5 core metrics)
/// </summary>
public class EvaluationMetrics
{
    /// <summary>
    /// Accuracy: Percentage of responses with required keywords/expected content
    /// Formula: (correct_responses / total_queries) * 100
    /// Target: > 80%
    /// </summary>
    public double Accuracy { get; set; }

    /// <summary>
    /// Hallucination Rate: Percentage of responses with forbidden keywords (fabricated info)
    /// Formula: (hallucinated_responses / total_queries) * 100
    /// Target: < 10%
    /// </summary>
    public double HallucinationRate { get; set; }

    /// <summary>
    /// Average Confidence: Mean RAG search confidence across all queries
    /// Formula: Sum(confidence) / total_queries
    /// Target: > 0.70
    /// </summary>
    public double AvgConfidence { get; set; }

    /// <summary>
    /// Citation Correctness: Percentage of responses with correct citations
    /// Formula: (correct_citations / total_queries_with_citations) * 100
    /// Target: > 80%
    /// </summary>
    public double CitationCorrectness { get; set; }

    /// <summary>
    /// Average Latency: Mean response time in milliseconds
    /// Formula: Sum(latency_ms) / total_queries
    /// Target: < 3000ms
    /// </summary>
    public double AvgLatencyMs { get; set; }
}

/// <summary>
/// Result for a single query evaluation
/// </summary>
public class QueryEvaluationResult
{
    /// <summary>Test case ID</summary>
    public required string TestCaseId { get; set; }

    /// <summary>Query executed</summary>
    public required string Query { get; set; }

    /// <summary>Response received</summary>
    public required string Response { get; set; }

    /// <summary>Response confidence score</summary>
    public double Confidence { get; set; }

    /// <summary>Response latency in milliseconds</summary>
    public int LatencyMs { get; set; }

    /// <summary>Whether response met accuracy criteria (keywords/content)</summary>
    public bool IsAccurate { get; set; }

    /// <summary>Whether response contained forbidden keywords (hallucination)</summary>
    public bool IsHallucinated { get; set; }

    /// <summary>Whether citations were correct</summary>
    public bool AreCitationsCorrect { get; set; }

    /// <summary>Detailed evaluation notes</summary>
    public string? Notes { get; set; }
}

/// <summary>
/// A/B comparison result between two prompt versions
/// </summary>
public class PromptComparisonResult
{
    /// <summary>Comparison run ID</summary>
    public required string ComparisonId { get; set; }

    /// <summary>Template ID</summary>
    public required string TemplateId { get; set; }

    /// <summary>Baseline version (usually current active)</summary>
    public required PromptEvaluationResult BaselineResult { get; set; }

    /// <summary>Candidate version (new version to test)</summary>
    public required PromptEvaluationResult CandidateResult { get; set; }

    /// <summary>Delta metrics (candidate - baseline)</summary>
    public required MetricDeltas Deltas { get; set; }

    /// <summary>Recommendation: Activate, Reject, or Manual Review</summary>
    public required ComparisonRecommendation Recommendation { get; set; }

    /// <summary>Reasoning for recommendation</summary>
    public string? RecommendationReason { get; set; }
}

/// <summary>
/// Delta metrics for A/B comparison
/// </summary>
public class MetricDeltas
{
    /// <summary>Change in accuracy percentage</summary>
    public double AccuracyDelta { get; set; }

    /// <summary>Change in hallucination rate percentage</summary>
    public double HallucinationRateDelta { get; set; }

    /// <summary>Change in average confidence</summary>
    public double AvgConfidenceDelta { get; set; }

    /// <summary>Change in citation correctness percentage</summary>
    public double CitationCorrectnessDelta { get; set; }

    /// <summary>Change in average latency (ms)</summary>
    public double AvgLatencyMsDelta { get; set; }
}

/// <summary>
/// Comparison recommendation
/// </summary>
public enum ComparisonRecommendation
{
    /// <summary>Candidate is significantly better, recommend activation</summary>
    Activate,

    /// <summary>Candidate is significantly worse, recommend rejection</summary>
    Reject,

    /// <summary>Results are mixed or marginal, require manual review</summary>
    ManualReview
}

// Admin API Request DTOs

/// <summary>
/// Request to evaluate a prompt version
/// </summary>
public class EvaluatePromptRequest
{
    /// <summary>Path to test dataset JSON file</summary>
    public required string DatasetPath { get; set; }

    /// <summary>Whether to store results in database (default: true)</summary>
    public bool StoreResults { get; set; } = true;
}

/// <summary>
/// Request to compare two prompt versions
/// </summary>
public class ComparePromptsRequest
{
    /// <summary>Baseline version ID (usually current active)</summary>
    public required string BaselineVersionId { get; set; }

    /// <summary>Candidate version ID (new version to test)</summary>
    public required string CandidateVersionId { get; set; }

    /// <summary>Path to test dataset JSON file</summary>
    public required string DatasetPath { get; set; }
}

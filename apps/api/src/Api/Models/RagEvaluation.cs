using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Api.Models;

/// <summary>
/// AI-06: Models for offline RAG evaluation
/// </summary>

/// <summary>
/// Single evaluation query with ground truth
/// </summary>
public record RagEvaluationQuery
{
    /// <summary>
    /// Unique identifier for this query
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Game identifier for filtering results
    /// </summary>
    public string GameId { get; init; } = string.Empty;

    /// <summary>
    /// The question or query text
    /// </summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// Ground truth answer (for semantic comparison if needed)
    /// </summary>
    public string? GroundTruthAnswer { get; init; }

    /// <summary>
    /// IDs of relevant documents (PDF IDs) that should be retrieved
    /// Used to calculate precision and recall
    /// </summary>
    public IReadOnlyList<string> RelevantDocIds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Difficulty level: easy, medium, hard
    /// </summary>
    public string? Difficulty { get; init; }

    /// <summary>
    /// Category of the query (e.g., "setup", "gameplay", "scoring")
    /// </summary>
    public string? Category { get; init; }
}

/// <summary>
/// Dataset containing multiple evaluation queries
/// </summary>
public record RagEvaluationDataset
{
    /// <summary>
    /// Dataset name/identifier
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Dataset version
    /// </summary>
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// Creation date
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// List of evaluation queries
    /// </summary>
    public IReadOnlyList<RagEvaluationQuery> Queries { get; init; } = Array.Empty<RagEvaluationQuery>();

    /// <summary>
    /// Optional metadata
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Result for a single query evaluation
/// </summary>
public record RagEvaluationQueryResult
{
    /// <summary>
    /// Query ID
    /// </summary>
    public string QueryId { get; init; } = string.Empty;

    /// <summary>
    /// Query text
    /// </summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// Number of documents retrieved
    /// </summary>
    public int RetrievedCount { get; init; }

    /// <summary>
    /// Number of relevant documents in the ground truth
    /// </summary>
    public int RelevantCount { get; init; }

    /// <summary>
    /// Number of relevant documents retrieved (true positives)
    /// </summary>
    public int RelevantRetrievedCount { get; init; }

    /// <summary>
    /// Precision@1: Relevance of top result
    /// </summary>
    public double PrecisionAt1 { get; init; }

    /// <summary>
    /// Precision@3: Relevance in top 3 results
    /// </summary>
    public double PrecisionAt3 { get; init; }

    /// <summary>
    /// Precision@5: Relevance in top 5 results
    /// </summary>
    public double PrecisionAt5 { get; init; }

    /// <summary>
    /// Precision@10: Relevance in top 10 results
    /// </summary>
    public double PrecisionAt10 { get; init; }

    /// <summary>
    /// Recall@K: Proportion of relevant docs retrieved
    /// </summary>
    public double RecallAtK { get; init; }

    /// <summary>
    /// Reciprocal rank of first relevant result (0 if none found)
    /// </summary>
    public double ReciprocalRank { get; init; }

    /// <summary>
    /// Query latency in milliseconds
    /// </summary>
    public double LatencyMs { get; init; }

    /// <summary>
    /// Retrieved document IDs in order
    /// </summary>
    public IReadOnlyList<string> RetrievedDocIds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Average confidence score of retrieved results
    /// </summary>
    public double? AverageConfidence { get; init; }

    /// <summary>
    /// Whether the query succeeded without errors
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Error message if query failed
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Aggregated evaluation report with statistics
/// </summary>
public record RagEvaluationReport
{
    /// <summary>
    /// Dataset name
    /// </summary>
    public string DatasetName { get; init; } = string.Empty;

    /// <summary>
    /// Evaluation timestamp
    /// </summary>
    public DateTime EvaluatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Total number of queries evaluated
    /// </summary>
    public int TotalQueries { get; init; }

    /// <summary>
    /// Number of successful queries
    /// </summary>
    public int SuccessfulQueries { get; init; }

    /// <summary>
    /// Number of failed queries
    /// </summary>
    public int FailedQueries { get; init; }

    /// <summary>
    /// Mean Reciprocal Rank across all queries
    /// </summary>
    public double MeanReciprocalRank { get; init; }

    /// <summary>
    /// Average Precision@1
    /// </summary>
    public double AvgPrecisionAt1 { get; init; }

    /// <summary>
    /// Average Precision@3
    /// </summary>
    public double AvgPrecisionAt3 { get; init; }

    /// <summary>
    /// Average Precision@5
    /// </summary>
    public double AvgPrecisionAt5 { get; init; }

    /// <summary>
    /// Average Precision@10
    /// </summary>
    public double AvgPrecisionAt10 { get; init; }

    /// <summary>
    /// Average Recall@K
    /// </summary>
    public double AvgRecallAtK { get; init; }

    /// <summary>
    /// Latency p50 (median) in milliseconds
    /// </summary>
    public double LatencyP50 { get; init; }

    /// <summary>
    /// Latency p95 (95th percentile) in milliseconds
    /// </summary>
    public double LatencyP95 { get; init; }

    /// <summary>
    /// Latency p99 (99th percentile) in milliseconds
    /// </summary>
    public double LatencyP99 { get; init; }

    /// <summary>
    /// Average latency in milliseconds
    /// </summary>
    public double AvgLatencyMs { get; init; }

    /// <summary>
    /// Average confidence score
    /// </summary>
    public double? AvgConfidence { get; init; }

    /// <summary>
    /// Per-query results
    /// </summary>
    public IReadOnlyList<RagEvaluationQueryResult> QueryResults { get; init; } = Array.Empty<RagEvaluationQueryResult>();

    /// <summary>
    /// Whether the evaluation passed quality thresholds
    /// </summary>
    public bool PassedQualityGates { get; init; }

    /// <summary>
    /// Quality gate failures (empty if passed)
    /// </summary>
    public IReadOnlyList<string> QualityGateFailures { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Optional metadata
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Quality thresholds for RAG evaluation
/// </summary>
public record RagQualityThresholds
{
    /// <summary>
    /// Minimum acceptable Precision@5 (default: 0.70)
    /// </summary>
    public double MinPrecisionAt5 { get; init; } = 0.70;

    /// <summary>
    /// Minimum acceptable Mean Reciprocal Rank (default: 0.60)
    /// </summary>
    public double MinMeanReciprocalRank { get; init; } = 0.60;

    /// <summary>
    /// Maximum acceptable latency p95 in ms (default: 2000ms)
    /// </summary>
    public double MaxLatencyP95Ms { get; init; } = 2000.0;

    /// <summary>
    /// Minimum success rate (default: 0.95, i.e., 95%)
    /// </summary>
    public double MinSuccessRate { get; init; } = 0.95;
}

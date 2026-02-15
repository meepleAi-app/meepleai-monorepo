using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.RagExecution;

/// <summary>
/// Query to compare two RAG executions side-by-side.
/// Issue #4459: RAG Query Replay.
/// </summary>
public record CompareRagExecutionsQuery(
    Guid ExecutionId1,
    Guid ExecutionId2,
    Guid UserId
) : IRequest<RagExecutionComparisonDto>;

/// <summary>
/// Side-by-side comparison of two RAG executions.
/// </summary>
public record RagExecutionComparisonDto
{
    public required ExecutionSummaryDto Execution1 { get; init; }
    public required ExecutionSummaryDto Execution2 { get; init; }
    public required MetricsDeltaDto MetricsDelta { get; init; }
    public required IReadOnlyList<BlockComparisonDto> BlockComparisons { get; init; }
    public required IReadOnlyList<DocumentDiffDto> DocumentDiffs { get; init; }
}

/// <summary>
/// Summary of a single execution for comparison.
/// </summary>
public record ExecutionSummaryDto
{
    public required Guid Id { get; init; }
    public required string TestQuery { get; init; }
    public required bool Success { get; init; }
    public required int TotalDurationMs { get; init; }
    public required int TotalTokensUsed { get; init; }
    public required decimal TotalCost { get; init; }
    public required int BlocksExecuted { get; init; }
    public required int BlocksFailed { get; init; }
    public string? FinalResponse { get; init; }
    public string? ConfigOverridesJson { get; init; }
    public Guid? ParentExecutionId { get; init; }
    public required DateTime ExecutedAt { get; init; }
}

/// <summary>
/// Delta between two execution metrics.
/// </summary>
public record MetricsDeltaDto
{
    /// <summary>Positive = execution2 took longer</summary>
    public required int DurationDeltaMs { get; init; }
    /// <summary>Positive = execution2 used more tokens</summary>
    public required int TokensDelta { get; init; }
    /// <summary>Positive = execution2 cost more</summary>
    public required decimal CostDelta { get; init; }
    /// <summary>"improved", "degraded", or "unchanged"</summary>
    public required string OverallAssessment { get; init; }
}

/// <summary>
/// Comparison of a single block across two executions.
/// </summary>
public record BlockComparisonDto
{
    public required string BlockId { get; init; }
    public required string BlockType { get; init; }
    public required string BlockName { get; init; }
    public BlockMetricsDto? Execution1 { get; init; }
    public BlockMetricsDto? Execution2 { get; init; }
    /// <summary>"improved", "degraded", "unchanged", or "added"/"removed"</summary>
    public required string Status { get; init; }
}

/// <summary>
/// Metrics for a single block execution.
/// </summary>
public record BlockMetricsDto
{
    public required bool Success { get; init; }
    public required int DurationMs { get; init; }
    public required int TokensUsed { get; init; }
    public required decimal Cost { get; init; }
    public double? ValidationScore { get; init; }
    public int? DocumentCount { get; init; }
}

/// <summary>
/// Diff of retrieved documents between two executions.
/// </summary>
public record DocumentDiffDto
{
    public required string BlockId { get; init; }
    public required IReadOnlyList<string> OnlyInExecution1 { get; init; }
    public required IReadOnlyList<string> OnlyInExecution2 { get; init; }
    public required IReadOnlyList<string> InBoth { get; init; }
    public required IReadOnlyList<ScoreChangeDto> ScoreChanges { get; init; }
}

/// <summary>
/// Change in document relevance score between executions.
/// </summary>
public record ScoreChangeDto
{
    public required string DocumentId { get; init; }
    public required double Score1 { get; init; }
    public required double Score2 { get; init; }
    /// <summary>Score2 - Score1. Positive = improved.</summary>
    public required double Delta { get; init; }
}

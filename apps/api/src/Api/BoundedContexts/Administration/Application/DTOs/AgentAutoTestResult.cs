namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Result of an automated agent test suite run.
/// Contains individual test case results and an overall quality report.
/// </summary>
public sealed class AgentAutoTestResult
{
    public required Guid GameId { get; init; }
    public required string GameTitle { get; init; }
    public required List<TestCaseResult> TestCases { get; init; }
    public required AgentQualityReport Report { get; init; }
    public required DateTime ExecutedAt { get; init; }
}

/// <summary>
/// Result for a single test question asked to the agent.
/// </summary>
public sealed class TestCaseResult
{
    public required int Index { get; init; }
    public required string Question { get; init; }
    public string? Answer { get; init; }
    public required double ConfidenceScore { get; init; }
    public required int LatencyMs { get; init; }
    public required int ChunksRetrieved { get; init; }
    public required bool Passed { get; init; }
    public string? FailureReason { get; init; }
}

/// <summary>
/// Aggregated quality report with grading.
/// Grade: A (>80% pass, avg confidence >0.7), B (>60%), C (>40%), F (&lt;40%)
/// </summary>
public sealed class AgentQualityReport
{
    public required int TotalTests { get; init; }
    public required int Passed { get; init; }
    public required int Failed { get; init; }
    public required double AverageConfidence { get; init; }
    public required int AverageLatencyMs { get; init; }
    public required string OverallGrade { get; init; }
    public required double PassRate { get; init; }
}

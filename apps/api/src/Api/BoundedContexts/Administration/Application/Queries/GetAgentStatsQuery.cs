using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

public sealed record GetAgentStatsQuery : IQuery<AgentStatsResult>
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? AgentName { get; init; }
    public bool? IsActive { get; init; }
}

public sealed record AgentStatsResult
{
    public required List<AgentStatDto> Agents { get; init; }
    public required AgentAggregateStats Totals { get; init; }
}

public sealed record AgentStatDto
{
    public required string AgentName { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required string ModelProvider { get; init; }
    public required string ModelName { get; init; }
    public required bool IsActive { get; init; }
    public required int ExecutionCount { get; init; }
    public required int TotalTokens { get; init; }
    public required int InputTokens { get; init; }
    public required int OutputTokens { get; init; }
    public required double AverageLatencyMs { get; init; }
    public required double SuccessRate { get; init; }
    public required DateTime? LastExecutedAt { get; init; }
    public required List<TokenTimeSeriesPoint> TokensOverTime { get; init; }
    public required List<LatencyTimeSeriesPoint> LatencyOverTime { get; init; }
}

public sealed record AgentAggregateStats
{
    public required int TotalAgents { get; init; }
    public required int ActiveAgents { get; init; }
    public required int TotalExecutions { get; init; }
    public required int TotalTokens { get; init; }
    public required double AverageLatency { get; init; }
}

public sealed record TokenTimeSeriesPoint
{
    public required DateTime Date { get; init; }
    public required int InputTokens { get; init; }
    public required int OutputTokens { get; init; }
    public required int TotalTokens { get; init; }
}

public sealed record LatencyTimeSeriesPoint
{
    public required DateTime Date { get; init; }
    public required double AverageLatencyMs { get; init; }
    public required int ExecutionCount { get; init; }
}

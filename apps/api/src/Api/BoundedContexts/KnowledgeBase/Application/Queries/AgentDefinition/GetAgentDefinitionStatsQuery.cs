using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.AgentDefinition;

/// <summary>
/// Query to get aggregated statistics for AgentDefinitions.
/// Issue #3708: Provides overview of agent templates in AI Lab.
/// </summary>
public sealed record GetAgentDefinitionStatsQuery : IQuery<AgentDefinitionStatsResult>
{
    public bool ActiveOnly { get; init; }
}

/// <summary>
/// Result containing AgentDefinition statistics.
/// </summary>
public sealed record AgentDefinitionStatsResult
{
    public required int TotalDefinitions { get; init; }
    public required int ActiveDefinitions { get; init; }
    public required int InactiveDefinitions { get; init; }
    public required List<TypeDistribution> DistributionByType { get; init; }
    public required List<AgentDefinitionSummary> RecentDefinitions { get; init; }
    public required DateTime? OldestCreatedAt { get; init; }
    public required DateTime? NewestCreatedAt { get; init; }
}

/// <summary>
/// Distribution of agent definitions by type.
/// </summary>
public sealed record TypeDistribution
{
    public required string Type { get; init; }
    public required int Count { get; init; }
    public required int ActiveCount { get; init; }
}

/// <summary>
/// Summary of an individual agent definition.
/// </summary>
public sealed record AgentDefinitionSummary
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime CreatedAt { get; init; }
}

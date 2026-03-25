using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get aggregated model metrics from agent test results.
/// Replaces the hardcoded mock data in AdminAgentAnalyticsEndpoints.
/// </summary>
internal record GetAgentModelsQuery() : IRequest<AgentModelsResult>;

/// <summary>
/// Result wrapper for agent model metrics.
/// </summary>
internal record AgentModelsResult(IReadOnlyList<AgentModelMetricsDto> Models);

/// <summary>
/// DTO representing aggregated metrics for a single AI model.
/// </summary>
internal record AgentModelMetricsDto(
    string Id,
    string Provider,
    string Name,
    bool Enabled,
    decimal CostPer1k,
    double AvgLatency,
    int Usage);

using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get aggregated agent metrics with optional filters.
/// Issue #3382: Agent Metrics Dashboard.
/// </summary>
internal record GetAgentMetricsQuery(
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    Guid? TypologyId = null,
    string? Strategy = null
) : IRequest<AgentMetricsDto>;

/// <summary>
/// Query to get metrics for a single agent/typology.
/// Issue #3382: Agent Metrics Dashboard.
/// </summary>
internal record GetSingleAgentMetricsQuery(
    Guid TypologyId,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null
) : IRequest<SingleAgentMetricsDto?>;

/// <summary>
/// Query to get top agents by usage or cost.
/// Issue #3382: Agent Metrics Dashboard.
/// </summary>
internal record GetTopAgentsQuery(
    int Limit = 10,
    string SortBy = "invocations", // "invocations", "cost", "confidence"
    DateOnly? StartDate = null,
    DateOnly? EndDate = null
) : IRequest<IReadOnlyList<TopAgentDto>>;

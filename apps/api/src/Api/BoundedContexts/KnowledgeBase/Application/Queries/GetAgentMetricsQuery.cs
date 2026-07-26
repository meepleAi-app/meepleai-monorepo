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

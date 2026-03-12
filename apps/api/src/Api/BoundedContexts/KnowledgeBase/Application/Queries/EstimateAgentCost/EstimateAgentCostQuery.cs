using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.EstimateAgentCost;

/// <summary>
/// Query to estimate the token cost before an admin starts chatting with an agent.
/// Calculates cost based on documents that will be used for RAG retrieval.
/// </summary>
internal sealed record EstimateAgentCostQuery(
    Guid GameId,
    List<Guid> DocumentIds,
    string StrategyName = "HybridSearch"
) : IRequest<AgentCostEstimateDto>;

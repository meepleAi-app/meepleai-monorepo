using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetAllAgentsQuery.
/// Retrieves all agents with optional filtering.
/// </summary>
public class GetAllAgentsQueryHandler : IRequestHandler<GetAllAgentsQuery, List<AgentDto>>
{
    private readonly IAgentRepository _agentRepository;
    private readonly ILogger<GetAllAgentsQueryHandler> _logger;

    public GetAllAgentsQueryHandler(
        IAgentRepository agentRepository,
        ILogger<GetAllAgentsQueryHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<AgentDto>> Handle(
        GetAllAgentsQuery request,
        CancellationToken cancellationToken)
    {
        List<Agent> agents;

        if (request.Type != null)
        {
            // Filter by type
            var agentType = AgentType.Parse(request.Type);
            agents = await _agentRepository.GetByTypeAsync(agentType, cancellationToken).ConfigureAwait(false);
        }
        else if (request.ActiveOnly == true)
        {
            // Only active agents
            agents = await _agentRepository.GetAllActiveAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Get all agents (active and inactive)
            agents = await _agentRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Retrieved {Count} agents", agents.Count);

        return agents.Select(ToDto).ToList();
    }

    private static AgentDto ToDto(Agent agent)
    {
        return new AgentDto(
            Id: agent.Id,
            Name: agent.Name,
            Type: agent.Type.Value,
            StrategyName: agent.Strategy.Name,
            StrategyParameters: agent.Strategy.Parameters,
            IsActive: agent.IsActive,
            CreatedAt: agent.CreatedAt,
            LastInvokedAt: agent.LastInvokedAt,
            InvocationCount: agent.InvocationCount,
            IsRecentlyUsed: agent.IsRecentlyUsed,
            IsIdle: agent.IsIdle
        );
    }
}

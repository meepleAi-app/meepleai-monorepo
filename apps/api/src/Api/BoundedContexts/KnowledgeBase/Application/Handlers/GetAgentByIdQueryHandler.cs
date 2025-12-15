using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetAgentByIdQuery.
/// Retrieves a single agent by ID.
/// </summary>
internal class GetAgentByIdQueryHandler : IRequestHandler<GetAgentByIdQuery, AgentDto?>
{
    private readonly IAgentRepository _agentRepository;
    private readonly ILogger<GetAgentByIdQueryHandler> _logger;

    public GetAgentByIdQueryHandler(
        IAgentRepository agentRepository,
        ILogger<GetAgentByIdQueryHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentDto?> Handle(
        GetAgentByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var agent = await _agentRepository.GetByIdAsync(request.AgentId, cancellationToken).ConfigureAwait(false);

        if (agent == null)
        {
            _logger.LogInformation("Agent not found: {AgentId}", request.AgentId);
            return null;
        }

        return ToDto(agent);
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

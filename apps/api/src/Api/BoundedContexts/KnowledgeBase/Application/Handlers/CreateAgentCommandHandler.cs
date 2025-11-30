using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for CreateAgentCommand.
/// Creates a new agent with specified configuration.
/// </summary>
public class CreateAgentCommandHandler : IRequestHandler<CreateAgentCommand, AgentDto>
{
    private readonly IAgentRepository _agentRepository;
    private readonly ILogger<CreateAgentCommandHandler> _logger;

    public CreateAgentCommandHandler(
        IAgentRepository agentRepository,
        ILogger<CreateAgentCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentDto> Handle(
        CreateAgentCommand request,
        CancellationToken cancellationToken)
    {
        // Validate name uniqueness
        var exists = await _agentRepository.ExistsAsync(request.Name, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            throw new InvalidOperationException($"Agent with name '{request.Name}' already exists");
        }

        // Parse agent type
        var agentType = AgentType.Parse(request.AgentType);

        // Create strategy
        var strategy = AgentStrategy.Custom(request.StrategyName, request.StrategyParameters);

        // Create agent aggregate
        var agent = new Agent(
            id: Guid.NewGuid(),
            name: request.Name,
            type: agentType,
            strategy: strategy,
            isActive: request.IsActive
        );

        // Persist
        await _agentRepository.AddAsync(agent, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created agent {AgentId} with name '{Name}' and type '{Type}'",
            agent.Id,
            agent.Name,
            agent.Type.Value);

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

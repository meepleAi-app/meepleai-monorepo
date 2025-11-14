using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for ConfigureAgentCommand.
/// Applies DDD pattern: Domain logic in aggregate, handler orchestrates.
/// </summary>
public class ConfigureAgentCommandHandler : IRequestHandler<ConfigureAgentCommand, ConfigureAgentResult>
{
    private readonly IAgentRepository _agentRepository;
    private readonly ILogger<ConfigureAgentCommandHandler> _logger;

    public ConfigureAgentCommandHandler(
        IAgentRepository agentRepository,
        ILogger<ConfigureAgentCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ConfigureAgentResult> Handle(
        ConfigureAgentCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Retrieve agent
            var agent = await _agentRepository.GetByIdAsync(request.AgentId, cancellationToken);

            if (agent == null)
            {
                _logger.LogWarning("Agent not found: {AgentId}", request.AgentId);
                return new ConfigureAgentResult(
                    Success: false,
                    Message: $"Agent with ID {request.AgentId} not found",
                    AgentId: request.AgentId,
                    ErrorCode: "AGENT_NOT_FOUND"
                );
            }

            // Create new strategy
            var strategy = AgentStrategy.Custom(request.StrategyName, request.StrategyParameters);

            // Apply domain logic (Configure method on aggregate)
            agent.Configure(strategy);

            // Persist changes
            await _agentRepository.UpdateAsync(agent, cancellationToken);

            _logger.LogInformation(
                "Agent {AgentId} configured with strategy {StrategyName}",
                request.AgentId,
                request.StrategyName);

            return new ConfigureAgentResult(
                Success: true,
                Message: $"Agent '{agent.Name}' configured successfully with strategy '{request.StrategyName}'",
                AgentId: request.AgentId
            );
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid configuration for agent {AgentId}", request.AgentId);
            return new ConfigureAgentResult(
                Success: false,
                Message: ex.Message,
                AgentId: request.AgentId,
                ErrorCode: "INVALID_CONFIGURATION"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring agent {AgentId}", request.AgentId);
            return new ConfigureAgentResult(
                Success: false,
                Message: "An error occurred while configuring the agent",
                AgentId: request.AgentId,
                ErrorCode: "INTERNAL_ERROR"
            );
        }
    }
}

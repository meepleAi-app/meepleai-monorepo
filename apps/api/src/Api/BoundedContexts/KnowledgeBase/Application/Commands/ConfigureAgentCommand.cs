using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to configure an agent with a new strategy.
/// Issue #866: AI Agents Entity & Configuration
/// </summary>
public record ConfigureAgentCommand(
    Guid AgentId,
    string StrategyName,
    Dictionary<string, object> StrategyParameters
) : IRequest<ConfigureAgentResult>;

/// <summary>
/// Result of configuring an agent.
/// </summary>
public record ConfigureAgentResult(
    bool Success,
    string Message,
    Guid AgentId,
    string? ErrorCode = null
);

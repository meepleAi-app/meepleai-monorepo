using MediatR;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to configure an agent with a new strategy.
/// Issue #866: AI Agents Entity & Configuration
/// </summary>
public record ConfigureAgentCommand(
    Guid AgentId,
    string StrategyName,
    IDictionary<string, object> StrategyParameters
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

using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to create a new agent.
/// Issue #866: AI Agents Entity & Configuration
/// </summary>
public record CreateAgentCommand(
    string Name,
    string AgentType,
    string StrategyName,
    Dictionary<string, object> StrategyParameters,
    bool IsActive = true
) : IRequest<AgentDto>;

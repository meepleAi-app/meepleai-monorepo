using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Orchestrated agent creation: validates slots, optionally adds game to library,
/// creates the agent, and creates an initial chat thread.
/// Issue #4772: Agent Creation Orchestration Flow.
/// </summary>
internal record CreateAgentWithSetupCommand(
    Guid UserId,
    string UserTier,
    string UserRole,
    Guid GameId,
    bool AddToCollection,
    string AgentType,
    string? AgentName,
    string? StrategyName,
    IDictionary<string, object>? StrategyParameters
) : IRequest<AgentCreationResultDto>;

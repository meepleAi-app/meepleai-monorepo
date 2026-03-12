using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Orchestrated agent creation: validates slots, optionally adds game to library,
/// creates the agent, creates an initial chat thread, and optionally links to a
/// SharedGame and attaches selected documents.
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
) : IRequest<AgentCreationResultDto>
{
    /// <summary>
    /// Optional SharedGame ID to link the agent to a community shared game.
    /// When provided, sends LinkAgentToSharedGameCommand after agent creation.
    /// </summary>
    public Guid? SharedGameId { get; init; }

    /// <summary>
    /// Optional list of document IDs to attach to the agent's knowledge base.
    /// When provided, sends UpdateAgentDocumentsCommand after agent creation.
    /// </summary>
    public List<Guid>? DocumentIds { get; init; }
}

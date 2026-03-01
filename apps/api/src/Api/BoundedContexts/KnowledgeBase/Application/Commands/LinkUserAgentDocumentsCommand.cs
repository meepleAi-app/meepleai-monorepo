using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to auto-link indexed VectorDocuments to an AgentDefinition when a user links an agent to a private game.
/// Issue #4941: Auto-link indexed PDF documents when creating game agent.
/// </summary>
/// <remarks>
/// No admin session required. Dispatched internally by LinkAgentToPrivateGameCommandHandler.
/// Fail-safe: if game has no indexed documents or agent definition is not found, the operation completes silently.
/// </remarks>
/// <param name="GameId">The private game ID (used to query VectorDocuments)</param>
/// <param name="AgentDefinitionId">The agent definition to update KbCardIds on</param>
internal sealed record LinkUserAgentDocumentsCommand(
    Guid GameId,
    Guid AgentDefinitionId) : IRequest<Unit>;

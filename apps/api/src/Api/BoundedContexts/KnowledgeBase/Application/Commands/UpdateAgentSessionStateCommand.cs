using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to update the current game state of an agent session.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// </summary>
internal record UpdateAgentSessionStateCommand(
    Guid AgentSessionId,
    string GameStateJson
) : IRequest;

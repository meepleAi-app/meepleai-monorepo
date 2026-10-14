using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to update the current game state of an agent session.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// Issue #4282: <c>GameSessionId</c> e <c>UserId</c> sono la sessione di gioco del percorso e chi
/// chiama; il gestore li confronta con la sessione agente (<see cref="AgentSessionAccess"/>).
/// </summary>
internal record UpdateAgentSessionStateCommand(
    Guid AgentSessionId,
    string GameStateJson,
    Guid GameSessionId,
    Guid UserId
) : IRequest;

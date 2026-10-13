using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to launch a new agent session for a game session.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// Issue #4154 (ADR-095 fetta 0b): <c>AgentDefinitionId</c> e` opzionale. Assente, l'handler usa
/// l'agente di sistema (ADR-094: un solo agente, non legato a un gioco), cosi` il client non deve
/// piu` scaricare un elenco di agenti per sceglierne uno.
/// </summary>
internal record LaunchSessionAgentCommand(
    Guid GameSessionId,
    Guid? AgentDefinitionId,
    Guid UserId,
    Guid GameId,
    string InitialGameStateJson
) : IRequest<Guid>;

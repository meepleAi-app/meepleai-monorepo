using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to update the agent definition of an active agent session.
/// Issue #4282: <c>GameSessionId</c> e <c>UserId</c> sono la sessione di gioco del percorso e chi
/// chiama; il gestore li confronta con la sessione agente (<see cref="AgentSessionAccess"/>).
/// </summary>
internal record UpdateAgentSessionDefinitionCommand(
    Guid AgentSessionId,
    Guid NewAgentDefinitionId,
    Guid GameSessionId,
    Guid UserId
) : IRequest;

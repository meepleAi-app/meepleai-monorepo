using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to update the runtime configuration of an active agent session.
/// Issue #3253 (BACK-AGT-002): PATCH Endpoint - Update Agent Runtime Config.
/// Issue #4282: <c>GameSessionId</c> e <c>UserId</c> sono la sessione di gioco del percorso e chi
/// chiama; il gestore li confronta con la sessione agente (<see cref="AgentSessionAccess"/>).
/// </summary>
internal record UpdateAgentSessionConfigCommand(
    Guid AgentSessionId,
    string ModelType,
    double Temperature,
    int MaxTokens,
    string RagStrategy,
    IDictionary<string, object> RagParams,
    Guid GameSessionId,
    Guid UserId
) : IRequest;

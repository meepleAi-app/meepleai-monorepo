using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to update the runtime configuration of an active agent session.
/// Issue #3253 (BACK-AGT-002): PATCH Endpoint - Update Agent Runtime Config.
/// </summary>
internal record UpdateAgentSessionConfigCommand(
    Guid AgentSessionId,
    string ModelType,
    double Temperature,
    int MaxTokens,
    string RagStrategy,
    IDictionary<string, object> RagParams
) : IRequest;

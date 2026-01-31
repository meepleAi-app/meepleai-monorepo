using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to update the agent typology of an active agent session.
/// Issue #3252 (BACK-AGT-001): PATCH Endpoint - Update Agent Typology.
/// </summary>
internal record UpdateAgentSessionTypologyCommand(
    Guid AgentSessionId,
    Guid NewTypologyId
) : IRequest;

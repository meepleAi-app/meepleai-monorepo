using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to update the agent definition of an active agent session.
/// </summary>
internal record UpdateAgentSessionDefinitionCommand(
    Guid AgentSessionId,
    Guid NewAgentDefinitionId
) : IRequest;

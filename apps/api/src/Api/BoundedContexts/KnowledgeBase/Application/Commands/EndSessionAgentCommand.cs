using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to end an agent session.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// </summary>
internal record EndSessionAgentCommand(
    Guid AgentSessionId
) : IRequest;

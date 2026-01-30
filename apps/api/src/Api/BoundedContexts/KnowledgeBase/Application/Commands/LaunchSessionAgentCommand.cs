using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to launch a new agent session for a game session.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// </summary>
internal record LaunchSessionAgentCommand(
    Guid GameSessionId,
    Guid TypologyId,
    Guid UserId,
    Guid AgentId,
    Guid GameId,
    string InitialGameStateJson
) : IRequest<Guid>;

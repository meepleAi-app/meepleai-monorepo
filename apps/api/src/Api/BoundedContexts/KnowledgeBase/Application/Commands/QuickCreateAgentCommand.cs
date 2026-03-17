using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to quickly create an agent + chat thread for a game after ownership is declared.
/// Automatically selects all indexed KB cards for the game.
/// </summary>
internal sealed record QuickCreateAgentCommand(
    Guid UserId,
    Guid GameId,
    Guid? SharedGameId,
    string UserRole,
    string UserTier
) : IRequest<QuickCreateAgentResult>;

/// <summary>
/// Result of quick agent creation.
/// </summary>
internal sealed record QuickCreateAgentResult(
    Guid AgentId,
    Guid ChatThreadId,
    string AgentName,
    int KbCardCount
);

using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to create a new chat session.
/// Issue #3483: Chat Session Persistence Service.
/// Issue #4913: Added AgentId/AgentType/AgentName + TierLimit for auto-archive.
/// </summary>
internal record CreateChatSessionCommand(
    Guid UserId,
    Guid GameId,
    string? Title = null,
    Guid? UserLibraryEntryId = null,
    Guid? AgentSessionId = null,
    string? AgentConfigJson = null,
    Guid? AgentId = null,
    string? AgentType = null,
    string? AgentName = null,
    int TierLimit = 0   // 0 = unlimited; > 0 triggers auto-archive when at limit
) : IRequest<Guid>;

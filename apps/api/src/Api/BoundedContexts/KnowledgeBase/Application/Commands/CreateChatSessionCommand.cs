using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to create a new chat session.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal record CreateChatSessionCommand(
    Guid UserId,
    Guid GameId,
    string? Title = null,
    Guid? UserLibraryEntryId = null,
    Guid? AgentSessionId = null,
    string? AgentConfigJson = null
) : IRequest<Guid>;

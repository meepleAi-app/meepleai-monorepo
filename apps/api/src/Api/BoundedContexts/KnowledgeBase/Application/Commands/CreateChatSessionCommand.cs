using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to create a new chat session.
/// Issue #3483: Chat Session Persistence Service.
/// Issue #4913: Added AgentId/AgentType/AgentName.
/// Issue #4212: the tier limit and its auto-archive are gone: they governed chat sessions, which
/// no page creates (the chat creates ChatThreads), and the admin page that edited the limit wrote
/// keys nobody read.
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
    string? UserRole = null
) : IRequest<Guid>;

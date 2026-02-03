using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to delete a chat session.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal record DeleteChatSessionCommand(
    Guid SessionId
) : IRequest;

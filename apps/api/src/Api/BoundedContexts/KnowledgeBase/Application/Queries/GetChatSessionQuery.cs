using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get a chat session with its messages.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal record GetChatSessionQuery(
    Guid SessionId,
    int MessageSkip = 0,
    int MessageTake = 50
) : IRequest<ChatSessionDto?>;

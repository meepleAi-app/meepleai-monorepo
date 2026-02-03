using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get chat sessions for a user and game combination.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal record GetUserGameChatSessionsQuery(
    Guid UserId,
    Guid GameId,
    int Skip = 0,
    int Take = 20
) : IRequest<ChatSessionListDto>;

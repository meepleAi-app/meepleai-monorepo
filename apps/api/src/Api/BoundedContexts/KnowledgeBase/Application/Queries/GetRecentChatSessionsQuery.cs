using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get recent chat sessions for a user.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal record GetRecentChatSessionsQuery(
    Guid UserId,
    int Limit = 10
) : IRequest<IReadOnlyList<ChatSessionSummaryDto>>;

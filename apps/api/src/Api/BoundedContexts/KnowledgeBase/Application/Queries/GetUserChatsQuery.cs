using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to retrieve all chat threads for a user.
/// </summary>
public record GetUserChatsQuery(
    Guid UserId,
    int Skip = 0,
    int Take = 50
) : IQuery<IReadOnlyList<ChatThreadDto>>;

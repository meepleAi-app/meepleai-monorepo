using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to retrieve chat threads for a specific game and user.
/// </summary>
internal record GetChatThreadsByGameQuery(
    Guid GameId,
    Guid UserId,
    int Skip = 0,
    int Take = 50
) : IQuery<IReadOnlyList<ChatThreadDto>>;

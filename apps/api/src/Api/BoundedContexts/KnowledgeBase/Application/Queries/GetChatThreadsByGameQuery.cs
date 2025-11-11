using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to retrieve chat threads for a specific game.
/// </summary>
public record GetChatThreadsByGameQuery(
    Guid GameId
) : IQuery<IReadOnlyList<ChatThreadDto>>;

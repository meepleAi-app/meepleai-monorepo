using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get the knowledge base (RAG) status for a game.
/// For private games: set IsPrivateGame = true so the handler filters by p.PrivateGameId.
/// For shared games: IsPrivateGame = false (default) — filters by p.GameId (existing behavior).
/// </summary>
internal sealed record GetKnowledgeBaseStatusQuery(
    Guid GameId,
    bool IsPrivateGame = false
) : IQuery<KnowledgeBaseStatusDto?>;

using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get the knowledge base (RAG) status for a game.
/// For private games: set IsPrivateGame = true so the handler filters by p.PrivateGameId.
/// For shared games: IsPrivateGame = false — filters by p.GameId (existing behavior).
/// </summary>
/// <remarks>
/// Issue #4137: <paramref name="RequestingUserId"/> and <paramref name="RequestingUserRole"/>
/// are REQUIRED, not optional. The handler authorizes every read through
/// <c>IRagAccessService.CanAccessRagAsync</c>, and an identity a caller may omit is an
/// identity some caller eventually will: before this, the private-game endpoint
/// authenticated and then threw the user id away, so any logged-in user could read the
/// KB status of any private game by id.
/// </remarks>
internal sealed record GetKnowledgeBaseStatusQuery(
    Guid GameId,
    Guid RequestingUserId,
    string RequestingUserRole,
    bool IsPrivateGame = false
) : IQuery<KnowledgeBaseStatusDto?>;

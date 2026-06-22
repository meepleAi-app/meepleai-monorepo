using Api.BoundedContexts.GameManagement.Application.DTOs.Leaderboard;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.Leaderboard;

/// <summary>
/// Query for a game's social leaderboard (#1467): the top players ranked by wins across the
/// play records visible to the caller (own records + records of the caller's groups).
/// </summary>
/// <param name="GameId">Catalog game id (route).</param>
/// <param name="CurrentUserId">Authenticated caller; determines visibility scope.</param>
/// <param name="Since">Optional lower bound on <c>SessionDate</c>.</param>
/// <param name="Limit">Top-N size (1..50, default 10).</param>
internal sealed record GetGameLeaderboardQuery(
    Guid GameId,
    Guid CurrentUserId,
    DateTime? Since = null,
    int Limit = 10
) : IQuery<GameLeaderboardResponse>;

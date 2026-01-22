using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetBadgeLeaderboard;

/// <summary>
/// Query to get the badge leaderboard with top contributors.
/// Issue #2728: Application - Badge Assignment Handlers
/// </summary>
internal sealed record GetBadgeLeaderboardQuery(
    LeaderboardPeriod Period = LeaderboardPeriod.AllTime,
    int PageNumber = 1,
    int PageSize = 10
) : IQuery<List<LeaderboardEntryDto>>;

/// <summary>
/// Leaderboard time period filter.
/// </summary>
public enum LeaderboardPeriod
{
    /// <summary>
    /// Last 7 days.
    /// </summary>
    Week,

    /// <summary>
    /// Last 30 days.
    /// </summary>
    Month,

    /// <summary>
    /// All time contributions.
    /// </summary>
    AllTime
}

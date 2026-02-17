using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.UserStats;

/// <summary>
/// Query to get dashboard statistics for current user (Issue #4578)
/// Epic #4575: Gaming Hub Dashboard - Phase 1
/// </summary>
internal record GetUserStatsQuery : IRequest<UserStatsDto>;

/// <summary>
/// DTO for user dashboard statistics (Issue #4578)
/// Displays quick stats: total games, monthly activity, weekly playtime, favorites
/// </summary>
internal record UserStatsDto(
    int TotalGames,
    int MonthlyPlays,
    int MonthlyPlaysChange,  // Percentage: +15 or -10
    TimeSpan WeeklyPlayTime,
    int MonthlyFavorites);

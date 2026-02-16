namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Lightweight DTO for admin overview statistics.
/// Issue #4198: Used by StatsOverview component.
/// </summary>
internal record AdminOverviewStatsDto(
    int TotalGames,
    int PublishedGames,
    int TotalUsers,
    int ActiveUsers,
    double ApprovalRate,
    int PendingApprovals,
    int RecentSubmissions);

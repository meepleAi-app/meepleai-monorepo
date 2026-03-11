namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Lightweight DTO for admin overview statistics.
/// Issue #4198: Used by StatsOverview component.
/// Issue #113: Added ActiveAiUsers for MAU-AI monitoring.
/// </summary>
internal record AdminOverviewStatsDto(
    int TotalGames,
    int PublishedGames,
    int TotalUsers,
    int ActiveUsers,
    int ActiveAiUsers,
    double ApprovalRate,
    int PendingApprovals,
    int RecentSubmissions);

namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// DTO for user library statistics (admin view).
/// Issue #3139 - Includes sessions played count for admin analytics.
/// </summary>
internal sealed record AdminUserLibraryStatsDto(
    int TotalGames,
    int FavoriteGames,
    int SessionsPlayed,
    DateTime? OldestAddedAt,
    DateTime? NewestAddedAt
);

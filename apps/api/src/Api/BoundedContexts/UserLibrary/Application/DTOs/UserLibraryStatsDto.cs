namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for library statistics.
/// Issue #3651: Added PrivatePdfs count for user collection dashboard.
/// </summary>
internal record UserLibraryStatsDto(
    int TotalGames,
    int FavoriteGames,
    int PrivatePdfs,
    DateTime? OldestAddedAt,
    DateTime? NewestAddedAt
);

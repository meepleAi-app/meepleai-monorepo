namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for library statistics.
/// </summary>
internal record UserLibraryStatsDto(
    int TotalGames,
    int FavoriteGames,
    DateTime? OldestAddedAt,
    DateTime? NewestAddedAt
);

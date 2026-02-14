namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Response DTO for trending games analytics
/// Issue #4310: Catalog Trending Analytics
/// </summary>
public record TrendingGamesResponseDto(
    IReadOnlyList<TrendingGameDto> Games,
    DateTime GeneratedAt,
    string Period
);

/// <summary>
/// Individual trending game data
/// </summary>
public record TrendingGameDto(
    Guid GameId,
    string Title,
    decimal TrendScore,
    decimal PercentageChange
);

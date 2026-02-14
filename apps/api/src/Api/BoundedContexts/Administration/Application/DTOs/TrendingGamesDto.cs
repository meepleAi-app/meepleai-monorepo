namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// DTO for a single trending game entry.
/// Issue #4310: Catalog Trending Analytics.
/// </summary>
internal record TrendingGameDto(
    Guid GameId,
    string Title,
    decimal TrendScore,
    decimal ChangePercent
);

/// <summary>
/// Response DTO for trending games query.
/// Issue #4310: Catalog Trending Analytics.
/// </summary>
internal record TrendingGamesResponseDto(
    IReadOnlyList<TrendingGameDto> Games,
    DateTime GeneratedAt,
    string Period
);

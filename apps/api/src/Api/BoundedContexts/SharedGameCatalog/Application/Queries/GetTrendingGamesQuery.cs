using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get trending games from catalog.
/// Issue #4310: Catalog Trending Analytics
/// </summary>
/// <param name="Period">Time period for trending calculation (week or month)</param>
/// <param name="Limit">Maximum number of games to return (default 10)</param>
public record GetTrendingGamesQuery(
    string Period = "week",
    int Limit = 10
) : IRequest<List<TrendingGameDto>>;

/// <summary>
/// DTO for trending game response.
/// </summary>
public record TrendingGameDto(
    Guid GameId,
    string Title,
    decimal TrendScore,
    decimal PercentageChange,
    string? ImageUrl
);

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;

/// <summary>
/// DTO for a recently added shared game.
/// Publisher is omitted — SharedGameEntity exposes it as a navigation collection,
/// not as a scalar property.
/// </summary>
internal sealed record NewGameDto(
    Guid Id,
    string Title,
    string? ImageUrl,
    DateTime CreatedAt,
    decimal? RatingAverage
);

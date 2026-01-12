using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Application;

/// <summary>
/// Data transfer object for shared game basic information.
/// </summary>
public sealed record SharedGameDto(
    Guid Id,
    int? BggId,
    string Title,
    int YearPublished,
    string Description,
    int MinPlayers,
    int MaxPlayers,
    int PlayingTimeMinutes,
    int MinAge,
    decimal? ComplexityRating,
    decimal? AverageRating,
    string ImageUrl,
    string ThumbnailUrl,
    GameStatus Status,
    DateTime CreatedAt,
    DateTime? ModifiedAt);

/// <summary>
/// Data transfer object for game rules.
/// </summary>
public sealed record GameRulesDto(
    string Content,
    string Language);

/// <summary>
/// Data transfer object for detailed shared game information.
/// </summary>
public sealed record SharedGameDetailDto(
    Guid Id,
    int? BggId,
    string Title,
    int YearPublished,
    string Description,
    int MinPlayers,
    int MaxPlayers,
    int PlayingTimeMinutes,
    int MinAge,
    decimal? ComplexityRating,
    decimal? AverageRating,
    string ImageUrl,
    string ThumbnailUrl,
    GameRulesDto? Rules,
    GameStatus Status,
    Guid CreatedBy,
    Guid? ModifiedBy,
    DateTime CreatedAt,
    DateTime? ModifiedAt);

using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to update an existing shared game in the catalog.
/// Supports core fields, taxonomy collections (categories, mechanics, designers, publishers),
/// and BggId. Null collections mean "do not change"; empty list means "clear".
/// </summary>
internal record UpdateSharedGameCommand(
    Guid GameId,
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
    Guid ModifiedBy,
    int? BggId = null,
    List<string>? Categories = null,
    List<string>? Mechanics = null,
    List<string>? Designers = null,
    List<string>? Publishers = null
) : ICommand<Unit>;

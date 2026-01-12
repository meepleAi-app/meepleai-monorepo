using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to create a new shared game in the catalog.
/// The game is created in Draft status and must be explicitly published.
/// </summary>
internal record CreateSharedGameCommand(
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
    Guid CreatedBy,
    int? BggId = null
) : ICommand<Guid>;

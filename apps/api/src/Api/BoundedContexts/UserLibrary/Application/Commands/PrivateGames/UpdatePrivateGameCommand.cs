using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;

/// <summary>
/// Command to update a private game's information.
/// Issue #3663: Phase 2 - Private Game CRUD Operations.
/// </summary>
/// <param name="PrivateGameId">ID of the private game to update</param>
/// <param name="UserId">ID of the user making the update (must be owner)</param>
/// <param name="Title">Updated game title</param>
/// <param name="MinPlayers">Updated minimum players</param>
/// <param name="MaxPlayers">Updated maximum players</param>
/// <param name="YearPublished">Updated year published</param>
/// <param name="Description">Updated description</param>
/// <param name="PlayingTimeMinutes">Updated playing time</param>
/// <param name="MinAge">Updated minimum age</param>
/// <param name="ComplexityRating">Updated complexity rating</param>
/// <param name="ImageUrl">Updated image URL</param>
internal record UpdatePrivateGameCommand(
    Guid PrivateGameId,
    Guid UserId,
    string Title,
    int MinPlayers,
    int MaxPlayers,
    int? YearPublished,
    string? Description,
    int? PlayingTimeMinutes,
    int? MinAge,
    decimal? ComplexityRating,
    string? ImageUrl
) : ICommand<PrivateGameDto>;

using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;

/// <summary>
/// Command to add a private game to user's library.
/// Issue #3663: Phase 2 - Private Game CRUD Operations.
/// </summary>
/// <param name="UserId">ID of the user adding the game</param>
/// <param name="Source">Source of the game data (Manual or BoardGameGeek)</param>
/// <param name="BggId">BoardGameGeek ID (required if Source is BoardGameGeek)</param>
/// <param name="Title">Game title (required)</param>
/// <param name="MinPlayers">Minimum number of players (required)</param>
/// <param name="MaxPlayers">Maximum number of players (required)</param>
/// <param name="YearPublished">Year the game was published (optional)</param>
/// <param name="Description">Game description (optional)</param>
/// <param name="PlayingTimeMinutes">Typical playing time in minutes (optional)</param>
/// <param name="MinAge">Minimum recommended age (optional)</param>
/// <param name="ComplexityRating">Complexity rating 1.0-5.0 (optional)</param>
/// <param name="ImageUrl">URL to game image (optional)</param>
/// <param name="ThumbnailUrl">URL to game thumbnail (optional, BGG only)</param>
internal record AddPrivateGameCommand(
    Guid UserId,
    string Source,
    int? BggId,
    string Title,
    int MinPlayers,
    int MaxPlayers,
    int? YearPublished = null,
    string? Description = null,
    int? PlayingTimeMinutes = null,
    int? MinAge = null,
    decimal? ComplexityRating = null,
    string? ImageUrl = null,
    string? ThumbnailUrl = null
) : ICommand<PrivateGameDto>;

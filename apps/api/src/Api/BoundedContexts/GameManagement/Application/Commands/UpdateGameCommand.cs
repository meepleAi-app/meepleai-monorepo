using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to update an existing game's details.
/// Issue #2255: Extended to support iconUrl and imageUrl updates.
/// </summary>
internal record UpdateGameCommand(
    Guid GameId,
    string? Title = null,
    string? Publisher = null,
    int? YearPublished = null,
    int? MinPlayers = null,
    int? MaxPlayers = null,
    int? MinPlayTimeMinutes = null,
    int? MaxPlayTimeMinutes = null,
    string? IconUrl = null,
    string? ImageUrl = null
) : ICommand<GameDto>;

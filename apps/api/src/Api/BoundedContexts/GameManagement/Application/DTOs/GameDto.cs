namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// Data transfer object for game information.
/// </summary>
public record GameDto(
    Guid Id,
    string Title,
    string? Publisher,
    int? YearPublished,
    int? MinPlayers,
    int? MaxPlayers,
    int? MinPlayTimeMinutes,
    int? MaxPlayTimeMinutes,
    int? BggId,
    DateTime CreatedAt
);

/// <summary>
/// DTO for creating a game.
/// </summary>
public record CreateGameRequest(
    string Title,
    string? Publisher = null,
    int? YearPublished = null,
    int? MinPlayers = null,
    int? MaxPlayers = null,
    int? MinPlayTimeMinutes = null,
    int? MaxPlayTimeMinutes = null
);

/// <summary>
/// DTO for updating game details.
/// </summary>
public record UpdateGameRequest(
    string? Title = null,
    string? Publisher = null,
    int? YearPublished = null,
    int? MinPlayers = null,
    int? MaxPlayers = null,
    int? MinPlayTimeMinutes = null,
    int? MaxPlayTimeMinutes = null
);

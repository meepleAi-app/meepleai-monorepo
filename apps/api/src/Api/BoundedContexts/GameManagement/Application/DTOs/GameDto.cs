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

/// <summary>
/// Extended DTO for game detail page with additional metadata and statistics.
/// </summary>
public record GameDetailsDto(
    Guid Id,
    string Title,
    string? Publisher,
    int? YearPublished,
    int? MinPlayers,
    int? MaxPlayers,
    int? MinPlayTimeMinutes,
    int? MaxPlayTimeMinutes,
    int? BggId,
    string? BggMetadata,
    DateTime CreatedAt,
    // Extended metadata
    bool SupportsSolo,
    // Play statistics (optional - null if no sessions exist)
    int? TotalSessionsPlayed,
    DateTime? LastPlayedAt
);

/// <summary>
/// DTO for rule atom (atomic rule element).
/// </summary>
public record RuleAtomDto(
    string Id,
    string Text,
    string? Section,
    string? Page,
    string? Line
);

/// <summary>
/// DTO for rule specification.
/// </summary>
public record RuleSpecDto(
    Guid Id,
    Guid GameId,
    string Version,
    DateTime CreatedAt,
    Guid? CreatedByUserId,
    Guid? ParentVersionId,
    IReadOnlyList<RuleAtomDto> Atoms
);

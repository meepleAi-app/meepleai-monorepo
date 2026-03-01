namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for private game entity.
/// Issue #3663: Phase 2 - Private Game CRUD Operations.
/// </summary>
/// <param name="Id">Private game ID</param>
/// <param name="OwnerId">ID of the user who owns this private game</param>
/// <param name="Source">Source of the game data (Manual or BoardGameGeek)</param>
/// <param name="BggId">BoardGameGeek ID if imported from BGG</param>
/// <param name="Title">Game title</param>
/// <param name="YearPublished">Year the game was published</param>
/// <param name="Description">Game description</param>
/// <param name="MinPlayers">Minimum number of players</param>
/// <param name="MaxPlayers">Maximum number of players</param>
/// <param name="PlayingTimeMinutes">Typical playing time in minutes</param>
/// <param name="MinAge">Minimum recommended age</param>
/// <param name="ComplexityRating">Complexity rating (1.0-5.0)</param>
/// <param name="ImageUrl">URL to game image</param>
/// <param name="ThumbnailUrl">URL to game thumbnail</param>
/// <param name="CreatedAt">When the private game was created</param>
/// <param name="UpdatedAt">When the private game was last updated</param>
/// <param name="BggSyncedAt">When BGG data was last synced (BGG-sourced games only)</param>
/// <param name="CanProposeToCatalog">Whether this game can be proposed to the shared catalog</param>
/// <param name="AgentDefinitionId">ID of the linked AgentDefinition, if any (Issue #4228)</param>
internal record PrivateGameDto(
    Guid Id,
    Guid OwnerId,
    string Source,
    int? BggId,
    string Title,
    int? YearPublished,
    string? Description,
    int MinPlayers,
    int MaxPlayers,
    int? PlayingTimeMinutes,
    int? MinAge,
    decimal? ComplexityRating,
    string? ImageUrl,
    string? ThumbnailUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? BggSyncedAt,
    bool CanProposeToCatalog,
    Guid? AgentDefinitionId = null
);

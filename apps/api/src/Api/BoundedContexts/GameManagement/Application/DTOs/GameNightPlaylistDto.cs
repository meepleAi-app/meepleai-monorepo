namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// Data transfer object for game night playlist.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal record GameNightPlaylistDto(
    Guid Id,
    string Name,
    DateTime? ScheduledDate,
    Guid CreatorUserId,
    bool IsShared,
    string? ShareToken,
    IReadOnlyList<PlaylistGameDto> Games,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// Data transfer object for a game within a playlist.
/// </summary>
internal record PlaylistGameDto(
    Guid SharedGameId,
    int Position,
    DateTime AddedAt
);

/// <summary>
/// Paginated response for playlist listing.
/// </summary>
internal record PaginatedPlaylistsResponse(
    IReadOnlyList<GameNightPlaylistDto> Playlists,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);

/// <summary>
/// Request DTO for creating a playlist.
/// </summary>
internal record CreatePlaylistRequest(
    string Name,
    DateTime? ScheduledDate = null
);

/// <summary>
/// Request DTO for updating a playlist.
/// </summary>
internal record UpdatePlaylistRequest(
    string? Name = null,
    DateTime? ScheduledDate = null
);

/// <summary>
/// Request DTO for adding a game to a playlist.
/// </summary>
internal record AddGameToPlaylistRequest(
    Guid SharedGameId,
    int Position
);

/// <summary>
/// Request DTO for reordering games in a playlist.
/// </summary>
internal record ReorderPlaylistGamesRequest(
    List<Guid> OrderedGameIds
);

/// <summary>
/// Response DTO for share link generation.
/// </summary>
internal record ShareLinkResponse(
    string ShareToken,
    string ShareUrl
);

namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO representing detailed game information from the user's library.
/// Combines data from Game, UserLibraryEntry, and BGG integration.
/// </summary>
internal record LibraryGameDetailDto(
    // Game base info
    Guid Id,
    string Title,
    int? YearPublished,
    string? ImageUrl,
    string? ThumbnailUrl,
    string? Description,
    int? MinPlayers,
    int? MaxPlayers,
    int? MinPlayTimeMinutes,
    int? MaxPlayTimeMinutes,

    // BGG data
    int? BggId,
    decimal? AverageRating,
    decimal? Complexity,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Mechanics,
    IReadOnlyList<string> Designers,

    // Library-specific
    Guid LibraryEntryId,
    string Status,
    bool IsFavorite,
    string? Notes,
    DateTime AddedAt,
    DateTime? StateChangedAt,
    string? StateNotes,
    IReadOnlyList<LabelDto> Labels,

    // Documents
    bool HasDocuments,
    int DocumentCount
);

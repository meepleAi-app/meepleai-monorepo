namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for a single game shown in downgrade preview.
/// </summary>
internal record LibraryDowngradeGameDto(
    Guid EntryId,
    Guid GameId,
    string GameTitle,
    string? GameImageUrl,
    bool IsFavorite,
    int TimesPlayed,
    DateTime AddedAt,
    DateTime? LastPlayedAt  // mapped from Stats.LastPlayed
);

/// <summary>
/// DTO for the full downgrade preview: games to keep vs games to remove when quota decreases.
/// </summary>
internal record LibraryForDowngradeDto(
    IReadOnlyList<LibraryDowngradeGameDto> GamesToKeep,
    IReadOnlyList<LibraryDowngradeGameDto> GamesToRemove
);

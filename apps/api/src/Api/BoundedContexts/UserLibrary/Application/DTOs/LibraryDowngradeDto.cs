namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

internal record LibraryForDowngradeDto(
    IReadOnlyList<LibraryDowngradeGameDto> GamesToKeep,
    IReadOnlyList<LibraryDowngradeGameDto> GamesToRemove
);

internal record LibraryDowngradeGameDto(
    Guid EntryId,
    Guid GameId,
    string GameTitle,
    string? GameImageUrl,
    bool IsFavorite,
    int TimesPlayed,
    DateTime AddedAt,
    DateTime? LastPlayedAt
);

namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for checking if a game is in the user's library.
/// </summary>
internal record GameInLibraryStatusDto(
    bool InLibrary,
    bool IsFavorite
);

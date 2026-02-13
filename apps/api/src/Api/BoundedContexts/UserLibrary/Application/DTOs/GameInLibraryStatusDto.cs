namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for checking if a game is in the user's library.
/// Issue #4259: Extended to include associated data for removal warnings.
/// </summary>
/// <param name="InLibrary">Whether the game is in the user's library.</param>
/// <param name="IsFavorite">Whether the game is marked as favorite.</param>
/// <param name="AssociatedData">Associated data that will be lost if removed. Null if not in library.</param>
internal record GameInLibraryStatusDto(
    bool InLibrary,
    bool IsFavorite,
    AssociatedDataDto? AssociatedData = null
);

/// <summary>
/// DTO containing counts of data associated with a library entry.
/// Used to display warning information before removal.
/// Issue #4259: Collection Quick Actions for MeepleCard
/// </summary>
/// <param name="HasCustomAgent">Whether the library entry has a custom AI agent configuration.</param>
/// <param name="HasPrivatePdf">Whether the library entry has a private PDF uploaded.</param>
/// <param name="ChatSessionsCount">Number of chat sessions associated with this game's agent.</param>
/// <param name="GameSessionsCount">Number of recorded game sessions (play history).</param>
/// <param name="ChecklistItemsCount">Number of setup checklist items.</param>
/// <param name="LabelsCount">Number of custom labels assigned to this game.</param>
internal record AssociatedDataDto(
    bool HasCustomAgent,
    bool HasPrivatePdf,
    int ChatSessionsCount,
    int GameSessionsCount,
    int ChecklistItemsCount,
    int LabelsCount
);

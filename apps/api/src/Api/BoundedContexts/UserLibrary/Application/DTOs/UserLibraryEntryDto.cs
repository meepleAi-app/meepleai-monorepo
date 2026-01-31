namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for a single library entry with game information.
/// </summary>
internal record UserLibraryEntryDto(
    Guid Id,
    Guid UserId,
    Guid GameId,
    string GameTitle,
    string? GamePublisher,
    int? GameYearPublished,
    string? GameIconUrl,
    string? GameImageUrl,
    DateTime AddedAt,
    string? Notes,
    bool IsFavorite,
    string CurrentState,
    DateTime? StateChangedAt = null,
    string? StateNotes = null,
    AgentConfigDto? CustomAgentConfig = null,
    CustomPdfDto? CustomPdf = null,
    bool HasPdfDocuments = false
);

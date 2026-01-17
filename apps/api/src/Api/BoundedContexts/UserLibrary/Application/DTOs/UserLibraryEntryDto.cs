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
    AgentConfigDto? CustomAgentConfig = null,
    CustomPdfDto? CustomPdf = null
);

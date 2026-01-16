namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO representing AI agent configuration for a game.
/// Used for both request (user input) and response (API output).
/// </summary>
public record AgentConfigDto(
    string LlmModel,
    double Temperature,
    int MaxTokens,
    string Personality,
    string DetailLevel,
    string? PersonalNotes = null
);

/// <summary>
/// DTO for custom PDF metadata response.
/// </summary>
public record CustomPdfDto(
    string Url,
    DateTime UploadedAt,
    long FileSizeBytes,
    string OriginalFileName
);

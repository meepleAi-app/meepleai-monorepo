namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for custom PDF metadata response.
/// </summary>
/// <remarks>
/// Issue #4138: this record lived in AgentConfigDto.cs next to the per-game agent configuration
/// DTO. That DTO is retired; this one moved to its own file unchanged.
/// </remarks>
public record CustomPdfDto(
    string Url,
    DateTime UploadedAt,
    long FileSizeBytes,
    string OriginalFileName
);

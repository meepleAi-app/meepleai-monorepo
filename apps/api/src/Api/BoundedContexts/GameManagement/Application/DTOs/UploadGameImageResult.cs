namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// Result of game image upload operation.
/// Issue #2255: Response DTO for file upload endpoint.
/// </summary>
public record UploadGameImageResult(
    bool Success,
    string? FileId,
    string? FileUrl,
    long FileSizeBytes,
    string? ErrorMessage = null
);

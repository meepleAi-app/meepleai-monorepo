namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// Result of a successful custom cover upload (L3).
/// </summary>
internal sealed record CustomCoverUploadResult(
    string CoverR2Key,
    string PresignedUrl
);

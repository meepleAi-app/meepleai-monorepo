namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Result of temporary PDF upload operation for game metadata extraction wizard.
/// Stores PDF in temporary location for processing during wizard flow.
/// </summary>
/// <param name="Success">Whether the upload succeeded</param>
/// <param name="FileId">Unique identifier for the uploaded file (null if failed)</param>
/// <param name="FilePath">Storage path of the uploaded file (null if failed)</param>
/// <param name="FileSizeBytes">Size of the uploaded file in bytes</param>
/// <param name="ErrorMessage">Error description if Success is false</param>
internal record TempPdfUploadResult(
    bool Success,
    Guid? FileId,
    string? FilePath,
    long FileSizeBytes,
    string? ErrorMessage = null);

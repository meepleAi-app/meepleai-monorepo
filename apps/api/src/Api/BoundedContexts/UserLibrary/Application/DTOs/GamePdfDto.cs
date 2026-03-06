namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for game PDF metadata.
/// Issue #3152: Game Detail Split View - PDF list for selector
/// </summary>
/// <param name="Id">PDF identifier (URL or document ID)</param>
/// <param name="Name">Display name (e.g., "Regolamento Base (IT)", "Espansione Pantheon")</param>
/// <param name="PageCount">Number of pages in PDF</param>
/// <param name="FileSizeBytes">File size in bytes</param>
/// <param name="UploadedAt">When PDF was uploaded/added</param>
/// <param name="Source">Source type: "Custom" (user-uploaded) or "Catalog" (from shared game)</param>
/// <param name="Language">Language code (IT, EN, etc.)</param>
/// <param name="ProcessingState">Pipeline state: Pending, Uploading, Extracting, Chunking, Embedding, Indexing, Ready, Failed</param>
public record GamePdfDto(
    string Id,
    string Name,
    int PageCount,
    long FileSizeBytes,
    DateTime UploadedAt,
    string Source,
    string? Language = null,
    string ProcessingState = "Pending"
);

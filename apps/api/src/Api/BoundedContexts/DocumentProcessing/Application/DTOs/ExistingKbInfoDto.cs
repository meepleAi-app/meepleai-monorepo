namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Metadata returned when a PDF with matching content hash already exists.
/// </summary>
internal record ExistingKbInfoDto(
    Guid PdfDocumentId,
    string Source,              // "user" | "shared"
    string FileName,
    string ProcessingState,     // "Ready" | "Embedding" | etc.
    int? TotalChunks,           // null if still processing
    string? OriginalGameName,
    Guid? SharedGameId
);

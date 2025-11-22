namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

public record PdfDocumentDto(
    Guid Id,
    Guid GameId,
    string FileName,
    string FilePath,
    long FileSizeBytes,
    string ProcessingStatus,
    DateTime UploadedAt,
    DateTime? ProcessedAt,
    int? PageCount
);

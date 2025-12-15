namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

internal record PdfDocumentDto(
    Guid Id,
    Guid GameId,
    string FileName,
    string FilePath,
    long FileSizeBytes,
    string ProcessingStatus,
    DateTime UploadedAt,
    DateTime? ProcessedAt,
    int? PageCount,
    string DocumentType = "base", // Issue #2051: base, expansion, errata, homerule
    bool IsPublic = false // Admin Wizard: Public library visibility
);

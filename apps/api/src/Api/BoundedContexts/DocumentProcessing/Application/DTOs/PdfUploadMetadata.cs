namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Metadata for PDF upload with game auto-creation support.
/// Allows users to upload PDFs and automatically create or reuse games based on version characteristics.
/// </summary>
internal record PdfUploadMetadata(
    string GameName,
    string VersionType,  // base, expansion, errata, home_rule
    string Language,     // it, en, de, fr, es, etc.
    string VersionNumber // 1.0, 2.0, 1.5, etc.
);

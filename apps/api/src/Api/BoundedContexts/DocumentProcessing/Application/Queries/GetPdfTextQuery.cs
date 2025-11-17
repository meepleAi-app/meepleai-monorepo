using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to get extracted text from a PDF document.
/// Returns full text content for display/download.
/// </summary>
public sealed record GetPdfTextQuery(
    Guid PdfId
) : IQuery<PdfTextResult?>;

/// <summary>
/// Result containing PDF text and metadata.
/// </summary>
public record PdfTextResult(
    Guid Id,
    string FileName,
    string? ExtractedText,
    string ProcessingStatus,
    DateTime? ProcessedAt,
    int? PageCount,
    int? CharacterCount,
    string? ProcessingError
);

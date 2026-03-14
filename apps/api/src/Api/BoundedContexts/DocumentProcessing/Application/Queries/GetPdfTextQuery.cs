using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to get extracted text from a PDF document.
/// Returns full text content for display/download.
/// </summary>
internal sealed record GetPdfTextQuery(
    Guid PdfId
) : IQuery<PdfTextResult?>;

/// <summary>
/// Result containing PDF text and metadata.
/// </summary>
internal record PdfTextResult(
    Guid Id,
    string FileName,
    string? ExtractedText,
    string ProcessingState,
    DateTime? ProcessedAt,
    int? PageCount,
    int? CharacterCount,
    string? ProcessingError
);

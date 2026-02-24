using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Returns a map of PdfDocument Id → (ProcessingState, ProgressPercentage, CanRetry, ErrorCategory, ProcessingError, FileName, FileSizeBytes)
/// for batch KB card status enrichment (Issue #5188).
/// </summary>
internal record GetPdfDocumentStatusesByIdsQuery(
    IReadOnlyList<Guid> DocumentIds
) : IQuery<IReadOnlyDictionary<Guid, PdfDocumentStatusResult>>;

/// <summary>Lightweight status result for KB card enrichment.</summary>
internal record PdfDocumentStatusResult(
    string FileName,
    long FileSizeBytes,
    string ProcessingState,
    int ProgressPercentage,
    bool CanRetry,
    string? ErrorCategory,
    string? ProcessingError
);



#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// Service for performing Optical Character Recognition (OCR) on PDF documents
/// </summary>
internal interface IOcrService
{
    /// <summary>
    /// Performs OCR on a specific page of a PDF document
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file</param>
    /// <param name="pageIndex">Zero-based page index</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OCR result containing extracted text and confidence score</returns>
    Task<OcrResult> ExtractTextFromPageAsync(
        string pdfPath,
        int pageIndex,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs OCR on an entire PDF document
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OCR result containing extracted text from all pages and average confidence</returns>
    Task<OcrResult> ExtractTextFromPdfAsync(
        string pdfPath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an OCR operation
/// </summary>
internal record OcrResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string ExtractedText { get; init; } = string.Empty;
    public float MeanConfidence { get; init; } // 0.0 - 1.0
    public int PageCount { get; init; }

    public static OcrResult CreateSuccess(string text, float confidence, int pageCount = 1) =>
        new()
        {
            Success = true,
            ExtractedText = text,
            MeanConfidence = confidence,
            PageCount = pageCount
        };

    public static OcrResult CreateFailure(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}


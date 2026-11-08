using Api.BoundedContexts.DocumentProcessing.Domain.Services;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// Adapter interface for PDF text extraction infrastructure
/// Abstracts the underlying PDF processing library (Docnet.Core, which reads the text layer and does not OCR)
/// </summary>
internal interface IPdfTextExtractor
{
    /// <summary>
    /// Extracts text from a PDF file
    /// </summary>
    /// <param name="pdfStream">PDF file stream</param>
    /// <param name="enableOcrFallback">Kept for the callers that pass it; <see cref="DocnetPdfTextExtractor"/> ignores it, it never performs OCR (#4437)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extraction result with text and metadata</returns>
    Task<TextExtractionResult> ExtractTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts text from a PDF file page-by-page
    /// </summary>
    /// <param name="pdfStream">PDF file stream</param>
    /// <param name="enableOcrFallback">Kept for the callers that pass it; <see cref="DocnetPdfTextExtractor"/> ignores it, it never performs OCR (#4437)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extraction result with page-aware chunks</returns>
    Task<PagedTextExtractionResult> ExtractPagedTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of text extraction operation
/// </summary>
internal record TextExtractionResult(
    bool Success,
    string ExtractedText,
    int PageCount,
    int CharacterCount,
    bool OcrTriggered,
    ExtractionQuality Quality,
    string? ErrorMessage = null,
    // #3589: true when the failure is permanent (e.g. Unstructured 413 — the file will
    // never shrink on retry) so callers can classify it as non-retryable instead of
    // leaving it eligible for RetryFailedPdfsJob's automatic backoff retries.
    bool IsPermanentFailure = false)
{
    public static TextExtractionResult CreateSuccess(
        string extractedText,
        int pageCount,
        int characterCount,
        bool ocrTriggered,
        ExtractionQuality quality)
    {
        return new TextExtractionResult(
            Success: true,
            ExtractedText: extractedText,
            PageCount: pageCount,
            CharacterCount: characterCount,
            OcrTriggered: ocrTriggered,
            Quality: quality,
            ErrorMessage: null);
    }

    public static TextExtractionResult CreateFailure(string errorMessage, bool isPermanentFailure = false)
    {
        return new TextExtractionResult(
            Success: false,
            ExtractedText: string.Empty,
            PageCount: 0,
            CharacterCount: 0,
            OcrTriggered: false,
            Quality: ExtractionQuality.VeryLow,
            ErrorMessage: errorMessage,
            IsPermanentFailure: isPermanentFailure);
    }
}

/// <summary>
/// Result of paged text extraction operation
/// </summary>
internal record PagedTextExtractionResult(
    bool Success,
    IList<PageTextChunk> PageChunks,
    int TotalPages,
    int TotalCharacters,
    bool OcrTriggered,
    string? ErrorMessage = null,
    IReadOnlyList<ExtractedElement>? StructuredElements = null,
    // #3589: see TextExtractionResult.IsPermanentFailure.
    bool IsPermanentFailure = false)
{
    public static PagedTextExtractionResult CreateSuccess(
        IList<PageTextChunk> pageChunks,
        int totalPages,
        int totalCharacters,
        bool ocrTriggered,
        IReadOnlyList<ExtractedElement>? structuredElements = null)
    {
        return new PagedTextExtractionResult(
            Success: true,
            PageChunks: pageChunks,
            TotalPages: totalPages,
            TotalCharacters: totalCharacters,
            OcrTriggered: ocrTriggered,
            ErrorMessage: null,
            StructuredElements: structuredElements);
    }

    public static PagedTextExtractionResult CreateFailure(string errorMessage, bool isPermanentFailure = false)
    {
        return new PagedTextExtractionResult(
            Success: false,
            PageChunks: Array.Empty<PageTextChunk>(),
            TotalPages: 0,
            TotalCharacters: 0,
            OcrTriggered: false,
            ErrorMessage: errorMessage,
            IsPermanentFailure: isPermanentFailure);
    }
}

/// <summary>
/// Represents text extracted from a single page
/// </summary>
/// <param name="PageNumber">The 1-based page of the PDF the text came from, or an estimate of it (see <paramref name="PageIsEstimated"/>).</param>
/// <param name="Text">The text of the page.</param>
/// <param name="CharStartIndex">Offset of the first character of the page in the extractor's own text.</param>
/// <param name="CharEndIndex">Offset of the last character of the page in the extractor's own text.</param>
/// <param name="PageIsEstimated">
/// True when <see cref="PageNumber"/> was not read from the document but derived (#4782): the Unstructured extractor
/// cuts the whole text into equal parts and numbers them 1..N. Such a number says nothing about where the text sits
/// in the PDF, so nobody may build a page map from it.
/// </param>
internal record PageTextChunk(
    int PageNumber,
    string Text,
    int CharStartIndex,
    int CharEndIndex,
    bool PageIsEstimated = false)
{
    /// <summary>
    /// Whether the page contains no extractable text
    /// </summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);

    /// <summary>
    /// Number of characters in this page
    /// </summary>
    public int CharCount => Text?.Length ?? 0;
}


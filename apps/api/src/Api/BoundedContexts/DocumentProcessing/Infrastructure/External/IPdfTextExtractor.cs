using Api.BoundedContexts.DocumentProcessing.Domain.Services;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// Adapter interface for PDF text extraction infrastructure
/// Abstracts the underlying PDF processing library (Docnet.Core)
/// </summary>
public interface IPdfTextExtractor
{
    /// <summary>
    /// Extracts text from a PDF file with optional OCR fallback
    /// </summary>
    /// <param name="pdfStream">PDF file stream</param>
    /// <param name="enableOcrFallback">Whether to enable OCR fallback for low-quality extraction</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Extraction result with text and metadata</returns>
    Task<TextExtractionResult> ExtractTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts text from a PDF file page-by-page with optional OCR fallback
    /// </summary>
    /// <param name="pdfStream">PDF file stream</param>
    /// <param name="enableOcrFallback">Whether to enable OCR fallback for low-quality extraction</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Extraction result with page-aware chunks</returns>
    Task<PagedTextExtractionResult> ExtractPagedTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken ct = default);
}

/// <summary>
/// Result of text extraction operation
/// </summary>
public record TextExtractionResult(
    bool Success,
    string ExtractedText,
    int PageCount,
    int CharacterCount,
    bool OcrTriggered,
    ExtractionQuality Quality,
    string? ErrorMessage = null)
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

    public static TextExtractionResult CreateFailure(string errorMessage)
    {
        return new TextExtractionResult(
            Success: false,
            ExtractedText: string.Empty,
            PageCount: 0,
            CharacterCount: 0,
            OcrTriggered: false,
            Quality: ExtractionQuality.VeryLow,
            ErrorMessage: errorMessage);
    }
}

/// <summary>
/// Result of paged text extraction operation
/// </summary>
public record PagedTextExtractionResult(
    bool Success,
    List<PageTextChunk> PageChunks,
    int TotalPages,
    int TotalCharacters,
    bool OcrTriggered,
    string? ErrorMessage = null)
{
    public static PagedTextExtractionResult CreateSuccess(
        List<PageTextChunk> pageChunks,
        int totalPages,
        int totalCharacters,
        bool ocrTriggered)
    {
        return new PagedTextExtractionResult(
            Success: true,
            PageChunks: pageChunks,
            TotalPages: totalPages,
            TotalCharacters: totalCharacters,
            OcrTriggered: ocrTriggered,
            ErrorMessage: null);
    }

    public static PagedTextExtractionResult CreateFailure(string errorMessage)
    {
        return new PagedTextExtractionResult(
            Success: false,
            PageChunks: new List<PageTextChunk>(),
            TotalPages: 0,
            TotalCharacters: 0,
            OcrTriggered: false,
            ErrorMessage: errorMessage);
    }
}

/// <summary>
/// Represents text extracted from a single page
/// </summary>
public record PageTextChunk(
    int PageNumber,
    string Text,
    int CharStartIndex,
    int CharEndIndex)
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

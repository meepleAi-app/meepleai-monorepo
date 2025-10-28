using Docnet.Core;
using Docnet.Core.Models;
using System.Text;
using System.Text.RegularExpressions;
using Api.Services.Exceptions;

namespace Api.Services;

/// <summary>
/// Service for extracting and normalizing text from PDF documents
/// Includes OCR fallback for scanned/image-based PDFs
/// </summary>
public class PdfTextExtractionService
{
    private readonly ILogger<PdfTextExtractionService> _logger;
    private readonly IOcrService? _ocrService;
    private readonly IConfiguration _configuration;

    // Quality threshold: if chars/page < this value, trigger OCR
    private const int DefaultOcrThresholdCharsPerPage = 100;

    // Semaphore to prevent concurrent access to Docnet.Core (not thread-safe)
    private static readonly SemaphoreSlim DocnetSemaphore = new(1, 1);

    public PdfTextExtractionService(
        ILogger<PdfTextExtractionService> logger,
        IConfiguration configuration,
        IOcrService? ocrService = null)
    {
        _logger = logger;
        _configuration = configuration;
        _ocrService = ocrService;
    }

    /// <summary>
    /// Extracts and normalizes text from a PDF file
    /// Automatically falls back to OCR if standard extraction produces poor results
    /// </summary>
    /// <param name="filePath">Path to the PDF file</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Extraction result containing normalized text and metadata</returns>
    public virtual async Task<PdfTextExtractionResult> ExtractTextAsync(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return PdfTextExtractionResult.CreateFailure("File path is required");
        }

        if (!File.Exists(filePath))
        {
            return PdfTextExtractionResult.CreateFailure($"File not found: {filePath}");
        }

        try
        {
            // Step 1: Try standard text extraction (with thread safety for Docnet.Core)
            await DocnetSemaphore.WaitAsync(ct);
            try
            {
                var (rawText, pageCount) = await Task.Run(() => ExtractRawText(filePath), ct);

                var normalizedText = NormalizeText(rawText);
                var charCount = normalizedText.Length;
                var charsPerPage = pageCount > 0 ? charCount / pageCount : 0;

                // Step 2: Check if OCR fallback is needed
                var ocrThreshold = _configuration.GetValue<int>(
                    "PdfExtraction:Ocr:ThresholdCharsPerPage",
                    DefaultOcrThresholdCharsPerPage);

                var needsOcr = charsPerPage < ocrThreshold && _ocrService != null;

                if (needsOcr)
                {
                    _logger.LogInformation(
                        "Standard extraction quality too low ({CharsPerPage} chars/page < {Threshold}). Falling back to OCR for {FilePath}",
                        charsPerPage, ocrThreshold, filePath);

                    // Fallback to OCR
                    var ocrResult = await _ocrService!.ExtractTextFromPdfAsync(filePath, ct);

                    if (!ocrResult.Success)
                    {
                        _logger.LogWarning(
                            "OCR fallback failed for {FilePath}: {Error}. Using standard extraction.",
                            filePath, ocrResult.ErrorMessage);

                        // Use standard extraction despite poor quality
                        return PdfTextExtractionResult.CreateSuccess(
                            normalizedText,
                            pageCount,
                            charCount,
                            usedOcr: false,
                            ocrConfidence: null);
                    }

                    var normalizedOcrText = NormalizeText(ocrResult.ExtractedText);

                    _logger.LogInformation(
                        "OCR extraction completed for {FilePath}. Pages: {PageCount}, Characters: {CharCount}, Confidence: {Confidence:F2}",
                        filePath, ocrResult.PageCount, normalizedOcrText.Length, ocrResult.MeanConfidence);

                    return PdfTextExtractionResult.CreateSuccess(
                        normalizedOcrText,
                        ocrResult.PageCount,
                        normalizedOcrText.Length,
                        usedOcr: true,
                        ocrConfidence: ocrResult.MeanConfidence);
                }

                // Standard extraction was good enough
                _logger.LogInformation(
                    "Extracted text from PDF: {FilePath}, Pages: {PageCount}, Characters: {CharCount}, Chars/Page: {CharsPerPage}",
                    filePath, pageCount, charCount, charsPerPage);

                return PdfTextExtractionResult.CreateSuccess(
                    normalizedText,
                    pageCount,
                    charCount,
                    usedOcr: false,
                    ocrConfidence: null);
            }
            finally
            {
                DocnetSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation extracting text from PDF: {FilePath}", filePath);
            throw new PdfExtractionException($"Invalid PDF operation: {ex.Message}", ex);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument extracting text from PDF: {FilePath}", filePath);
            throw new PdfExtractionException($"Invalid PDF argument: {ex.Message}", ex);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Unsupported PDF format: {FilePath}", filePath);
            throw new PdfExtractionException($"Unsupported PDF format: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error extracting text from PDF: {FilePath}", filePath);
            throw new PdfExtractionException($"Failed to read PDF file: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error extracting text from PDF: {FilePath}", filePath);
            throw new PdfExtractionException($"Failed to extract text from PDF: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extracts text from PDF with accurate page tracking (AI-08)
    /// Returns chunks at page granularity for downstream processing
    /// </summary>
    /// <param name="filePath">Path to the PDF file</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Extraction result with page-aware chunks</returns>
    public virtual async Task<PagedExtractionResult> ExtractPagedTextAsync(
        string filePath,
        CancellationToken ct = default)
    {
        // Validation: null or empty path
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return PagedExtractionResult.CreateFailure("File path is required");
        }

        // Validation: file existence
        if (!File.Exists(filePath))
        {
            return PagedExtractionResult.CreateFailure($"File not found: {filePath}");
        }

        try
        {
            // Use semaphore for thread safety (Docnet.Core is not thread-safe)
            await DocnetSemaphore.WaitAsync(ct);
            try
            {
                var pageChunks = await Task.Run(() => ExtractPagedRawText(filePath), ct);

                var totalPageCount = pageChunks.Count;
                var nonEmptyPageCount = pageChunks.Count(pc => !pc.IsEmpty);

                _logger.LogInformation(
                    "Extracted paged text from PDF: {FilePath}, Pages: {PageCount}, Non-empty pages: {NonEmptyCount}",
                    filePath, totalPageCount, nonEmptyPageCount);

                return PagedExtractionResult.CreateSuccess(pageChunks, totalPageCount);
            }
            finally
            {
                DocnetSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation extracting paged text from PDF: {FilePath}", filePath);
            throw new PdfExtractionException($"Invalid PDF operation: {ex.Message}", ex);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument extracting paged text from PDF: {FilePath}", filePath);
            throw new PdfExtractionException($"Invalid PDF argument: {ex.Message}", ex);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Unsupported PDF format for paged extraction: {FilePath}", filePath);
            throw new PdfExtractionException($"Unsupported PDF format: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error extracting paged text from PDF: {FilePath}", filePath);
            throw new PdfExtractionException($"Failed to read PDF file: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error extracting paged text from PDF: {FilePath}", filePath);
            throw new PdfExtractionException($"Failed to extract paged text from PDF: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extracts text from PDF page-by-page using Docnet.Core (AI-08)
    /// </summary>
    private List<PagedTextChunk> ExtractPagedRawText(string filePath)
    {
        var pageChunks = new List<PagedTextChunk>();

        using var library = DocLib.Instance;
        using var docReader = library.GetDocReader(filePath, new PageDimensions(1080, 1920));

        var pageCount = docReader.GetPageCount();

        for (int i = 0; i < pageCount; i++)
        {
            using var pageReader = docReader.GetPageReader(i);
            var pageText = pageReader.GetText();

            // Normalize text for this page
            var normalizedText = NormalizeText(pageText ?? string.Empty);

            // Create PagedTextChunk (page numbers are 1-indexed for user display)
            var pageChunk = new PagedTextChunk(
                Text: normalizedText,
                PageNumber: i + 1,  // 1-indexed
                CharStartIndex: 0,  // Always 0 for full-page extraction
                CharEndIndex: normalizedText.Length > 0 ? normalizedText.Length - 1 : 0
            );

            pageChunks.Add(pageChunk);
        }

        return pageChunks;
    }

    /// <summary>
    /// Extracts raw text from PDF using Docnet.Core
    /// </summary>
    private (string Text, int PageCount) ExtractRawText(string filePath)
    {
        var textBuilder = new StringBuilder();

        using var library = DocLib.Instance;
        using var docReader = library.GetDocReader(filePath, new PageDimensions(1080, 1920));

        var pageCount = docReader.GetPageCount();

        for (int i = 0; i < pageCount; i++)
        {
            using var pageReader = docReader.GetPageReader(i);
            var pageText = pageReader.GetText();

            if (!string.IsNullOrWhiteSpace(pageText))
            {
                textBuilder.AppendLine(pageText);
                textBuilder.AppendLine(); // Add separator between pages
            }
        }

        return (textBuilder.ToString(), pageCount);
    }

    /// <summary>
    /// Normalizes extracted text: fixes whitespace, paragraphs, and formatting
    /// </summary>
    private string NormalizeText(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        // Step 1: Normalize line endings
        var text = rawText.Replace("\r\n", "\n").Replace("\r", "\n");

        // Step 2: Remove excessive whitespace within lines
        text = Regex.Replace(text, @"[ \t]+", " ");

        // Step 3: Fix broken paragraphs (lines that end mid-word)
        // If a line ends without punctuation and next line starts lowercase, merge them
        text = Regex.Replace(text, @"([a-z,])\n([a-z])", "$1 $2");

        // Step 4: Normalize multiple newlines to paragraph breaks (max 2 newlines)
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        // Step 5: Trim leading/trailing whitespace from each line
        var lines = text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line));

        text = string.Join('\n', lines);

        // Step 6: Final trim
        return text.Trim();
    }

}

/// <summary>
/// Result of PDF text extraction operation
/// </summary>
public record PdfTextExtractionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string ExtractedText { get; init; } = string.Empty;
    public int PageCount { get; init; }
    public int CharacterCount { get; init; }
    public bool UsedOcr { get; init; }
    public float? OcrConfidence { get; init; } // 0.0 - 1.0, null if OCR not used

    public static PdfTextExtractionResult CreateSuccess(
        string text,
        int pageCount,
        int charCount,
        bool usedOcr = false,
        float? ocrConfidence = null) =>
        new()
        {
            Success = true,
            ExtractedText = text,
            PageCount = pageCount,
            CharacterCount = charCount,
            UsedOcr = usedOcr,
            OcrConfidence = ocrConfidence
        };

    public static PdfTextExtractionResult CreateFailure(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}

/// <summary>
/// Internal model: text extracted from a single page (AI-08)
/// </summary>
public record PagedTextChunk(
    string Text,
    int PageNumber,         // 1-indexed for user display
    int CharStartIndex,     // Position within page (always 0 for full-page extraction)
    int CharEndIndex        // Text.Length - 1
)
{
    /// <summary>
    /// True if the page contains no meaningful text
    /// </summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);
}

/// <summary>
/// Result of page-aware PDF text extraction (AI-08)
/// </summary>
public record PagedExtractionResult(
    bool Success,
    List<PagedTextChunk> PageChunks,
    int TotalPageCount,
    string? Error
)
{
    public static PagedExtractionResult CreateSuccess(
        List<PagedTextChunk> chunks,
        int pageCount) =>
        new(Success: true, PageChunks: chunks, TotalPageCount: pageCount, Error: null);

    public static PagedExtractionResult CreateFailure(string error) =>
        new(Success: false, PageChunks: new(), TotalPageCount: 0, Error: error);
}

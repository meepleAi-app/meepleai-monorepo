using Docnet.Core;
using Docnet.Core.Models;
using System.Text;
using System.Text.RegularExpressions;

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
            // Step 1: Try standard text extraction
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from PDF: {FilePath}", filePath);
            return PdfTextExtractionResult.CreateFailure($"Extraction failed: {ex.Message}");
        }
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

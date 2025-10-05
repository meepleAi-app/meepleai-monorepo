using Docnet.Core;
using Docnet.Core.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace Api.Services;

/// <summary>
/// Service for extracting and normalizing text from PDF documents
/// </summary>
public class PdfTextExtractionService
{
    private readonly ILogger<PdfTextExtractionService> _logger;

    public PdfTextExtractionService(ILogger<PdfTextExtractionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extracts and normalizes text from a PDF file
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
            var (rawText, pageCount) = await Task.Run(() => ExtractRawText(filePath), ct);

            if (string.IsNullOrWhiteSpace(rawText))
            {
                _logger.LogWarning("No text extracted from PDF: {FilePath}", filePath);
                return PdfTextExtractionResult.CreateSuccess(string.Empty, 0, 0);
            }

            var normalizedText = NormalizeText(rawText);
            var charCount = normalizedText.Length;

            _logger.LogInformation(
                "Extracted text from PDF: {FilePath}, Pages: {PageCount}, Characters: {CharCount}",
                filePath, pageCount, charCount);

            return PdfTextExtractionResult.CreateSuccess(normalizedText, pageCount, charCount);
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

    public static PdfTextExtractionResult CreateSuccess(string text, int pageCount, int charCount) =>
        new()
        {
            Success = true,
            ExtractedText = text,
            PageCount = pageCount,
            CharacterCount = charCount
        };

    public static PdfTextExtractionResult CreateFailure(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}

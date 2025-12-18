using System.Text;
using System.Text.RegularExpressions;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// Domain service for PDF text processing business rules
/// Encapsulates business logic for OCR decisions, text normalization, and quality assessment
/// </summary>
internal class PdfTextProcessingDomainService
{
    private readonly IConfiguration _configuration;

    // Business rule: OCR threshold for low-quality extraction detection
    private const int DefaultOcrThresholdCharsPerPage = 100;

    public PdfTextProcessingDomainService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Business rule: Determines if OCR fallback should be triggered based on extraction quality
    /// </summary>
    /// <param name="extractedText">Text extracted by standard method</param>
    /// <param name="pageCount">Number of pages in document</param>
    /// <returns>True if OCR should be triggered, false otherwise</returns>
    public bool ShouldTriggerOcr(string extractedText, int pageCount)
    {
        if (pageCount <= 0) return false;
        if (string.IsNullOrWhiteSpace(extractedText)) return true;

        var threshold = _configuration.GetValue<int>(
            "PdfExtraction:Ocr:ThresholdCharsPerPage",
            DefaultOcrThresholdCharsPerPage);

        var avgCharsPerPage = extractedText.Length / pageCount;
        return avgCharsPerPage < threshold;
    }

    /// <summary>
    /// Business rule: Normalizes extracted text for consistent downstream processing
    /// </summary>
    /// <param name="rawText">Raw extracted text</param>
    /// <returns>Normalized text</returns>
    public static string NormalizeText(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        // Step 1: Normalize line endings
        var text = rawText.Replace("\r\n", "\n").Replace("\r", "\n");

        // Step 2: Remove excessive whitespace within lines
        // FIX MA0009: Add timeout to prevent ReDoS attacks
        // FIX MA0023: Add ExplicitCapture to prevent capturing unneeded groups
        text = Regex.Replace(text, @"[ \t]+", " ", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

        // Step 3: Fix broken paragraphs (lines that end mid-word)
        // If a line ends with a letter (mid-word) and next line starts with a letter, merge them
        text = Regex.Replace(text, @"([a-z])\n([a-z])", "$1$2", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

        // Step 4: Normalize multiple newlines to paragraph breaks (max 2 newlines)
        text = Regex.Replace(text, @"\n{3,}", "\n\n", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

        // Step 5: Trim leading/trailing whitespace from each line (preserve empty lines for paragraph breaks)
        var lines = text.Split('\n')
            .Select(line => line.Trim());

        text = string.Join('\n', lines);

        // Step 6: Unicode normalization (Form C - canonical composition)
        text = text.Normalize(NormalizationForm.FormC);

        // Step 7: Remove zero-width characters (after Unicode normalization to avoid it adding them back)
        text = RemoveZeroWidthCharacters(text);

        // Step 8: Final trim
        return text.Trim();
    }

    /// <summary>
    /// Business rule: Assesses the quality of extracted text based on character density
    /// </summary>
    /// <param name="text">Extracted text</param>
    /// <param name="pageCount">Number of pages</param>
    /// <returns>Quality assessment</returns>
    public static ExtractionQuality AssessQuality(string text, int pageCount)
    {
        if (pageCount <= 0) return ExtractionQuality.VeryLow;
        if (string.IsNullOrWhiteSpace(text)) return ExtractionQuality.VeryLow;

        var avgCharsPerPage = text.Length / pageCount;

        if (avgCharsPerPage > 1000) return ExtractionQuality.High;
        if (avgCharsPerPage > 200) return ExtractionQuality.Medium;
        if (avgCharsPerPage > 50) return ExtractionQuality.Low;
        return ExtractionQuality.VeryLow; // Likely needs OCR
    }

    /// <summary>
    /// Removes zero-width characters that can interfere with text processing
    /// </summary>
    private static string RemoveZeroWidthCharacters(string text)
    {
        // Remove zero-width characters by replacing each one individually
        // This is more reliable than regex for unicode character removal
        return text
            .Replace("\u200B", string.Empty) // zero-width space
            .Replace("\u200C", string.Empty) // zero-width non-joiner
            .Replace("\u200D", string.Empty) // zero-width joiner
            .Replace("\uFEFF", string.Empty); // byte order mark (BOM)
    }
}

/// <summary>
/// Represents the quality of text extraction
/// </summary>
public enum ExtractionQuality
{
    /// <summary>Very low quality - likely needs OCR (&lt;50 chars/page)</summary>
    VeryLow,

    /// <summary>Low quality - may need OCR (50-200 chars/page)</summary>
    Low,

    /// <summary>Medium quality - acceptable (200-1000 chars/page)</summary>
    Medium,

    /// <summary>High quality - excellent (&gt;1000 chars/page)</summary>
    High
}

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Detects language from extracted text content.
/// Issue #5445: Auto-detect PDF language for pipeline routing.
/// </summary>
internal interface ILanguageDetector
{
    /// <summary>
    /// Detects the primary language of the given text.
    /// Returns ISO 639-1 code (e.g., "en", "it", "de") or null if detection fails.
    /// </summary>
    LanguageDetectionResult Detect(string text);
}

/// <summary>
/// Result of language detection including confidence and analyzability.
/// </summary>
internal sealed record LanguageDetectionResult(
    string DetectedLanguage,
    bool IsAnalyzable,
    double Confidence,
    string? RejectionReason = null);

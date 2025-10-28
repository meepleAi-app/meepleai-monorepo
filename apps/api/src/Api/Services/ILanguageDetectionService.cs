namespace Api.Services;

/// <summary>
/// Service for detecting language from text content
/// Supports 5 languages: English (en), Italian (it), German (de), French (fr), Spanish (es)
/// </summary>
public interface ILanguageDetectionService
{
    /// <summary>
    /// Detect the language of the provided text
    /// </summary>
    /// <param name="text">Text to analyze for language detection</param>
    /// <returns>
    /// ISO 639-1 language code (en, it, de, fr, es)
    /// Returns "en" (English) as default if:
    /// - Text is empty or whitespace
    /// - Language cannot be reliably detected
    /// - Detected language is not supported
    /// </returns>
    Task<string> DetectLanguageAsync(string text);

    /// <summary>
    /// Check if a language code is supported by the system
    /// </summary>
    /// <param name="languageCode">ISO 639-1 language code (case-insensitive)</param>
    /// <returns>True if language is supported, false otherwise</returns>
    bool IsSupportedLanguage(string? languageCode);
}

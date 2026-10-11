#pragma warning disable MA0048 // File name must match type name - the service and its detection result
namespace Api.Services;

/// <summary>
/// Service for detecting language from text content
/// Supports 5 languages: English (en), Italian (it), German (de), French (fr), Spanish (es)
/// </summary>
internal interface ILanguageDetectionService
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
    /// #4168: la lingua del testo e se il rilevamento è sicuro. Non è sicuro quando il testo ha meno
    /// di tre parole, quando nessuna lingua ha un punteggio significativo, o quando l'inglese vince
    /// solo per lo spareggio: in quei casi <see cref="DetectLanguageAsync"/> risponde «en» per default,
    /// e chi ha un'altra fonte (l'<c>Accept-Language</c> della richiesta) deve poterla preferire.
    /// </summary>
    LanguageDetection Detect(string text);

    /// <summary>
    /// Check if a language code is supported by the system
    /// </summary>
    /// <param name="languageCode">ISO 639-1 language code (case-insensitive)</param>
    /// <returns>True if language is supported, false otherwise</returns>
    bool IsSupportedLanguage(string? languageCode);
}

/// <summary>#4168: una lingua rilevata (codice ISO 639-1) e se il rilevamento è sicuro.</summary>
internal sealed record LanguageDetection(string Language, bool IsConfident);

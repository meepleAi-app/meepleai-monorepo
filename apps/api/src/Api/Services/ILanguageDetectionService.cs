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
    /// #4168: la lingua del testo e se il rilevamento è sicuro. Chi ha un'altra fonte (la lingua
    /// dell'interfaccia) la preferisce a un rilevamento non sicuro.
    /// <para>
    /// #4480: «sicuro» vuol dire abbastanza da smentire la lingua che l'interfaccia chiede, ed è un
    /// requisito alto: almeno tre parole, un margine netto sulla seconda lingua, e un secondo segnale
    /// indipendente che indica la stessa lingua. Su un testo senza parole riconosciute la lingua è «en»,
    /// come in <see cref="DetectLanguageAsync"/>, che restituisce sempre la stessa lingua di questo metodo.
    /// </para>
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

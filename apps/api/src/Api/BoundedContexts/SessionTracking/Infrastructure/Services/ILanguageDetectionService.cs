namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Heuristic NLP language detection for OCR text. Returns lang + confidence normalized [0,1].
/// Filters to allowlist EN, FR, DE, ES, IT; out-of-allowlist returns Lang=null.
/// Implementation must be thread-safe (singleton-scoped).
/// </summary>
internal interface ILanguageDetectionService
{
    /// <summary>Detects the language of the given OCR text using heuristic NLP.</summary>
    /// <param name="text">OCR-extracted text. Implementation must handle empty, single-char, very long inputs gracefully.</param>
    /// <returns>Detection result. On library exception, implementation returns (null, 0.0) — never throws.</returns>
    LanguageDetectionResult Detect(string text);
}

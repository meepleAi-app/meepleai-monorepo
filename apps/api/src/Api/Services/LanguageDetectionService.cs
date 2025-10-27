using LanguageDetection;

namespace Api.Services;

/// <summary>
/// Local language detection service using LanguageDetection library
/// Supports offline detection for 5 languages without external API calls
/// </summary>
public class LanguageDetectionService : ILanguageDetectionService
{
    private static readonly string[] SupportedLanguages = { "en", "it", "de", "fr", "es" };
    private readonly LanguageDetector _detector;
    private readonly ILogger<LanguageDetectionService> _logger;

    public LanguageDetectionService(ILogger<LanguageDetectionService> logger)
    {
        _logger = logger;

        // Initialize LanguageDetection detector
        _detector = new LanguageDetector();
        _detector.AddAllLanguages();

        _logger.LogInformation("LanguageDetectionService initialized with 5 supported languages: {Languages}",
            string.Join(", ", SupportedLanguages));
    }

    /// <summary>
    /// Detect language from text using LanguageDetection library (local, offline)
    /// </summary>
    public async Task<string> DetectLanguageAsync(string text)
    {
        // Handle empty or whitespace text
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Empty or whitespace text provided, defaulting to 'en'");
            return "en";
        }

        try
        {
            // Language detection is synchronous but CPU-bound, so run on thread pool
            var detectedLanguage = await Task.Run(() => _detector.Detect(text));

            if (string.IsNullOrEmpty(detectedLanguage))
            {
                _logger.LogWarning("Could not reliably detect language from text (length: {Length}), defaulting to 'en'",
                    text.Length);
                return "en";
            }

            // Normalize to ISO 639-1 (library returns 2-letter codes)
            var languageCode = detectedLanguage.ToLowerInvariant();

            if (!IsSupportedLanguage(languageCode))
            {
                _logger.LogWarning("Detected unsupported language '{Code}', defaulting to 'en'", languageCode);
                return "en";
            }

            _logger.LogInformation("Detected language: {Language} (text length: {Length})",
                languageCode, text.Length);

            return languageCode;
        }
        catch (Exception ex)
        {
            // FALLBACK PATTERN: Language detection failures default to English (en)
            // Rationale: Language detection is a best-effort enhancement for RAG and embedding
            // operations. Failing the entire operation because we cannot detect language would
            // reduce system availability. Defaulting to English is reasonable given it's the most
            // common language for board game rules and our primary training data language.
            // Context: Detection failures are typically edge cases (empty text, unsupported encoding)
            _logger.LogError(ex, "Error during language detection, defaulting to 'en'");
            return "en";
        }
    }

    /// <summary>
    /// Check if language code is in the supported list
    /// </summary>
    public bool IsSupportedLanguage(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return false;
        }

        return SupportedLanguages.Contains(languageCode.ToLowerInvariant());
    }
}

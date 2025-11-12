using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for detecting hallucinations via forbidden keyword analysis
/// ISSUE-972: BGAI-030 - Hallucination detection (forbidden keywords)
/// </summary>
/// <remarks>
/// Detects LLM hallucinations by checking for uncertainty phrases and admission keywords.
/// Supports 5 languages: English, Italian, German, French, Spanish (Italian-first design per ADR-002).
///
/// Hallucination indicators include:
/// - Uncertainty: "I don't know", "not sure", "unclear"
/// - Admission: "cannot find", "not specified"
/// - Hedging: "might be", "possibly", "perhaps"
///
/// Severity levels:
/// - None: 0 keywords (valid response)
/// - Low: 1-2 keywords (minor uncertainty)
/// - Medium: 3-4 keywords (significant uncertainty)
/// - High: 5+ keywords or critical phrases
/// </remarks>
public class HallucinationDetectionService : IHallucinationDetectionService
{
    private readonly ILanguageDetectionService? _languageDetection;
    private readonly ILogger<HallucinationDetectionService> _logger;

    // Multilingual forbidden keyword dictionaries (Italian-first per ADR-002)
    private static readonly Dictionary<string, HashSet<string>> ForbiddenKeywordsByLanguage = new()
    {
        // Italian (Primary language - ADR-002)
        ["it"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "non lo so",
            "non sono sicuro",
            "non sono sicura",
            "non riesco a trovare",
            "non specificato nelle regole",
            "non è specificato",
            "poco chiaro",
            "ambiguo",
            "potrebbe essere",
            "probabilmente",
            "forse",
            "non sono certo",
            "non sono certa",
            "non posso dire",
            "informazioni insufficienti"
        },

        // English
        ["en"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "I don't know",
            "I'm not sure",
            "I cannot find",
            "I can't find",
            "not specified",
            "not specified in the rules",
            "unclear",
            "ambiguous",
            "might be",
            "possibly",
            "perhaps",
            "I think",
            "not certain",
            "insufficient information"
        },

        // German
        ["de"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Ich weiß nicht",
            "Ich bin nicht sicher",
            "Ich kann nicht finden",
            "nicht angegeben",
            "nicht in den Regeln",
            "unklar",
            "vielleicht",
            "möglicherweise",
            "ungenügende Informationen"
        },

        // French
        ["fr"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Je ne sais pas",
            "Je ne suis pas sûr",
            "Je ne suis pas sûre",
            "Je ne trouve pas",
            "pas spécifié",
            "pas spécifié dans les règles",
            "peu clair",
            "peut-être",
            "possiblement",
            "informations insuffisantes"
        },

        // Spanish
        ["es"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "No lo sé",
            "No estoy seguro",
            "No estoy segura",
            "No puedo encontrar",
            "no especificado",
            "no especificado en las reglas",
            "poco claro",
            "tal vez",
            "posiblemente",
            "información insuficiente"
        }
    };

    public HallucinationDetectionService(
        ILogger<HallucinationDetectionService> logger,
        ILanguageDetectionService? languageDetection = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _languageDetection = languageDetection; // Optional - may not be available in tests
    }

    /// <inheritdoc/>
    public async Task<HallucinationValidationResult> DetectHallucinationsAsync(
        string responseText,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return new HallucinationValidationResult
            {
                IsValid = true,
                DetectedKeywords = new List<string>(),
                Language = language ?? "en",
                TotalKeywordsChecked = 0,
                Message = "Empty response (no keywords to check)",
                Severity = HallucinationSeverity.None
            };
        }

        // Auto-detect language if not provided and detection service available
        if (string.IsNullOrWhiteSpace(language) && _languageDetection != null)
        {
            language = await _languageDetection.DetectLanguageAsync(responseText);
            _logger.LogDebug("Auto-detected language: {Language}", language);
        }

        // Default to English if still null
        language ??= "en";

        // Get language-specific forbidden keywords
        if (!ForbiddenKeywordsByLanguage.TryGetValue(language, out var keywords))
        {
            _logger.LogWarning(
                "No forbidden keywords configured for language {Language}, using English fallback",
                language);
            keywords = ForbiddenKeywordsByLanguage["en"];
            language = "en";
        }

        // Detect forbidden keywords in response
        var detectedKeywords = new List<string>();
        foreach (var keyword in keywords)
        {
            if (responseText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                detectedKeywords.Add(keyword);
                _logger.LogDebug(
                    "Detected forbidden keyword '{Keyword}' in response (language: {Language})",
                    keyword, language);
            }
        }

        var isValid = detectedKeywords.Count == 0;
        var severity = CalculateSeverity(detectedKeywords, responseText);

        var message = isValid
            ? $"No hallucinations detected ({keywords.Count} keywords checked, language: {language})"
            : $"{detectedKeywords.Count} hallucination indicator(s) detected (language: {language})";

        if (!isValid)
        {
            _logger.LogWarning(
                "Hallucination detection: {Count} keyword(s) found in response (language: {Language}, severity: {Severity})",
                detectedKeywords.Count, language, severity);
        }

        return new HallucinationValidationResult
        {
            IsValid = isValid,
            DetectedKeywords = detectedKeywords,
            Language = language,
            TotalKeywordsChecked = keywords.Count,
            Message = message,
            Severity = severity
        };
    }

    /// <inheritdoc/>
    public int GetForbiddenKeywordCount(string language)
    {
        return ForbiddenKeywordsByLanguage.TryGetValue(language, out var keywords)
            ? keywords.Count
            : ForbiddenKeywordsByLanguage["en"].Count;
    }

    /// <summary>
    /// Calculate hallucination severity based on detected keywords
    /// </summary>
    private HallucinationSeverity CalculateSeverity(List<string> detectedKeywords, string responseText)
    {
        if (detectedKeywords.Count == 0)
        {
            return HallucinationSeverity.None;
        }

        // Critical phrases (explicit admission of ignorance)
        var criticalPhrases = new[]
        {
            "don't know", "non lo so", "ne sais pas", "weiß nicht", "no lo sé",
            "cannot find", "non riesco", "ne trouve pas", "kann nicht", "no puedo"
        };

        foreach (var phrase in criticalPhrases)
        {
            if (responseText.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return HallucinationSeverity.High;
            }
        }

        // Severity based on count
        return detectedKeywords.Count switch
        {
            >= 5 => HallucinationSeverity.High,
            >= 3 => HallucinationSeverity.Medium,
            >= 1 => HallucinationSeverity.Low,
            _ => HallucinationSeverity.None
        };
    }
}

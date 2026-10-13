using System.Linq;
using System.Text.RegularExpressions;

namespace Api.Services;

/// <summary>
/// Local language detection service using lightweight heuristics.
/// Supports offline detection for 5 languages without external API calls.
/// </summary>
internal class LanguageDetectionService : ILanguageDetectionService
{
    private static readonly string[] SupportedLanguages = { "en", "it", "de", "fr", "es" };
    // FIX MA0009: Add timeout to prevent ReDoS attacks
    private static readonly Regex WordRegex = new(@"\p{L}+", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly IReadOnlyDictionary<string, LanguageProfile> Profiles = new Dictionary<string, LanguageProfile>
(StringComparer.Ordinal)
    {
        ["en"] = CreateProfile(
            "en",
            new[]
            {
                "player", "players", "turn", "turns", "board", "card", "cards", "deck", "wins", "game", "trick", "score", "points", "play"
            },
            new[]
            {
                "the", "and", "with", "must", "any", "each", "until", "when", "will", "may", "cannot", "should"
            },
            Array.Empty<char>()),
        ["it"] = CreateProfile(
            "it",
            new[]
            {
                "giocatore", "giocatori", "turno", "turni", "scacchiera", "mazzo", "carta", "carte", "presa", "partita", "vince", "punti", "strategia"
            },
            new[]
            {
                "il", "lo", "la", "gli", "i", "le", "che", "con", "per", "se", "dei", "delle", "degli"
            },
            new[] { 'à', 'è', 'é', 'ì', 'ò', 'ù' }),
        ["de"] = CreateProfile(
            "de",
            new[]
            {
                "spieler", "spielerinnen", "zug", "züge", "brett", "karte", "karten", "stich", "farben", "runde", "gewinnt", "punkten", "spiel"
            },
            new[]
            {
                "der", "die", "das", "und", "mit", "den", "dem", "des", "ein", "eine", "nicht", "wenn", "dass"
            },
            new[] { 'ä', 'ö', 'ü', 'ß' }),
        ["fr"] = CreateProfile(
            "fr",
            new[]
            {
                "joueur", "joueurs", "tour", "tours", "plateau", "carte", "cartes", "partie", "gagne", "rangée", "stratégie", "points", "pièce", "pièces"
            },
            new[]
            {
                "le", "la", "les", "des", "une", "un", "et", "avec", "pour", "que", "qui", "dans", "sur", "ces"
            },
            new[] { 'à', 'â', 'ä', 'ç', 'é', 'è', 'ê', 'ë', 'î', 'ï', 'ô', 'ù', 'û', 'ü' }),
        ["es"] = CreateProfile(
            "es",
            new[]
            {
                "jugador", "jugadores", "turno", "turnos", "tablero", "carta", "cartas", "juego", "ganar", "gana", "baza", "puntos", "estrategia"
            },
            new[]
            {
                "el", "la", "los", "las", "una", "un", "con", "para", "que", "de", "del", "al", "por"
            },
            new[] { 'á', 'é', 'í', 'ñ', 'ó', 'ú', 'ü' })
    };

    private readonly ILogger<LanguageDetectionService> _logger;

    public LanguageDetectionService(ILogger<LanguageDetectionService> logger)
    {
        _logger = logger;

        _logger.LogInformation("LanguageDetectionService initialized with 5 supported languages: {Languages}",
            string.Join(", ", SupportedLanguages));
    }

    /// <summary>
    /// Detect language from text using heuristics (keywords, stop words, accent markers).
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
            // Language detection uses lightweight heuristics based on keywords, stop words, and accents.
            var detectedLanguage = await Task.Run(() => DetectCore(text).Language).ConfigureAwait(false);

            _logger.LogInformation("Detected language: {Language} (text length: {Length})",
                detectedLanguage, text.Length);

            return detectedLanguage;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
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

        return SupportedLanguages.Contains(languageCode.ToLowerInvariant(), StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public LanguageDetection Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new LanguageDetection("en", IsConfident: false);
        }

        try
        {
            return DetectCore(text);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // Come DetectLanguageAsync: il rilevamento è un aiuto, non deve far fallire una risposta.
            _logger.LogError(ex, "Error during language detection, defaulting to 'en'");
            return new LanguageDetection("en", IsConfident: false);
        }
    }

    private static LanguageDetection DetectCore(string text)
    {
        var lowercase = text.ToLowerInvariant();
        var tokens = WordRegex
            .Matches(lowercase)
            .Select(match => match.Value)
            .Where(token => token.Length > 1)
            .ToArray();

        if (tokens.Length < 3)
        {
            // Not enough signal to make a confident choice
            return new LanguageDetection("en", IsConfident: false);
        }

        var scores = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var profile in Profiles.Values)
        {
            double score = 0;

            foreach (var token in tokens)
            {
                if (profile.Keywords.Contains(token))
                {
                    score += 3;
                }
                else if (profile.StopWords.Contains(token))
                {
                    score += 1.5;
                }
            }

            if (profile.AccentCharacters.Count > 0)
            {
                var accentHits = lowercase.Count(ch => profile.AccentCharacters.Contains(ch));
                score += accentHits * 0.5;
            }

            scores[profile.Code] = score;
        }

        var best = scores
            .MaxBy(kv => kv.Value);

        // If no language has meaningful score, default to English.
        if (best.Value <= 0.5)
        {
            return new LanguageDetection("en", IsConfident: false);
        }

        // Avoid oscillation when scores are similar by preferring English unless another language is clearly dominant.
        if (!string.Equals(best.Key, "en", StringComparison.Ordinal) &&
            scores.TryGetValue("en", out var englishScore) &&
            best.Value - englishScore < 1.0)
        {
            return new LanguageDetection(englishScore >= best.Value ? "en" : best.Key, IsConfident: false);
        }

        return new LanguageDetection(best.Key, IsConfident: true);
    }

    private static LanguageProfile CreateProfile(string code, string[] keywords, string[] stopWords, char[] accentCharacters)
    {
        return new LanguageProfile(
            code,
            new HashSet<string>(keywords.Select(k => k.ToLowerInvariant()), StringComparer.Ordinal),
            new HashSet<string>(stopWords.Select(w => w.ToLowerInvariant()), StringComparer.Ordinal),
            new HashSet<char>(accentCharacters.Select(char.ToLowerInvariant)));
    }

    private sealed record LanguageProfile(
        string Code,
        HashSet<string> Keywords,
        HashSet<string> StopWords,
        HashSet<char> AccentCharacters);
}

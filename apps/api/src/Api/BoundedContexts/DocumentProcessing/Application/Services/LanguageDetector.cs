using System.Globalization;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Unicode script-based language detector for PDF content.
/// Issue #5445: Detects language from extracted text using character analysis.
/// No external dependencies — uses Unicode category analysis.
/// </summary>
internal sealed class LanguageDetector : ILanguageDetector
{
    /// <summary>
    /// Western-alphabet languages eligible for rulebook analysis pipeline.
    /// Issue #5445: Only these languages proceed to chunking/analysis.
    /// </summary>
    private static readonly HashSet<string> AnalyzableLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "en", "it", "de", "es", "fr", "pt", "nl", "pl",
        "sv", "da", "no", "fi", "cs", "hu", "ro", "hr", "el"
    };

    // Characters per script used for scoring
    private const int SampleSize = 2000; // Analyze first ~2000 chars for speed

    private static readonly char[] SplitChars = { ' ', '\n', '\r', '\t', '.', ',', '!', '?', ';', ':', '(', ')', '"' };

    public LanguageDetectionResult Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new LanguageDetectionResult(
                DetectedLanguage: "unknown",
                IsAnalyzable: false,
                Confidence: 0,
                RejectionReason: "No text content available for language detection");
        }

        var sample = text.Length > SampleSize ? text[..SampleSize] : text;
        var scriptCounts = AnalyzeScripts(sample);
        var totalLetters = scriptCounts.Values.Sum();

        if (totalLetters == 0)
        {
            return new LanguageDetectionResult(
                DetectedLanguage: "unknown",
                IsAnalyzable: false,
                Confidence: 0,
                RejectionReason: "Text contains no identifiable letter characters");
        }

        var dominantScript = scriptCounts.MaxBy(kv => kv.Value);
        var confidence = (double)dominantScript.Value / totalLetters;

        // Check for unsupported scripts
        if (dominantScript.Key is "cjk" or "arabic" or "hebrew" or "thai" or "devanagari")
        {
            var scriptName = dominantScript.Key switch
            {
                "cjk" => "CJK (Chinese/Japanese/Korean)",
                "arabic" => "Arabic",
                "hebrew" => "Hebrew",
                "thai" => "Thai",
                "devanagari" => "Devanagari (Hindi/Sanskrit)",
                _ => dominantScript.Key
            };

            return new LanguageDetectionResult(
                DetectedLanguage: MapScriptToLanguage(dominantScript.Key),
                IsAnalyzable: false,
                Confidence: confidence,
                RejectionReason: $"Currently only Western-alphabet languages are supported. Detected: {scriptName}");
        }

        // For Latin/Greek/Cyrillic scripts, try to identify specific language
        var detectedLang = DetectSpecificLanguage(sample, dominantScript.Key);
        var isAnalyzable = AnalyzableLanguages.Contains(detectedLang);

        return new LanguageDetectionResult(
            DetectedLanguage: detectedLang,
            IsAnalyzable: isAnalyzable,
            Confidence: confidence,
            RejectionReason: isAnalyzable ? null : $"Language '{detectedLang}' is not currently supported for analysis");
    }

    private static Dictionary<string, int> AnalyzeScripts(string text)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["latin"] = 0,
            ["cjk"] = 0,
            ["arabic"] = 0,
            ["hebrew"] = 0,
            ["greek"] = 0,
            ["cyrillic"] = 0,
            ["thai"] = 0,
            ["devanagari"] = 0
        };

        foreach (var ch in text)
        {
            if (!char.IsLetter(ch)) continue;

            var script = ClassifyCharacter(ch);
            if (script != null && counts.TryGetValue(script, out var current))
            {
                counts[script] = current + 1;
            }
        }

        return counts;
    }

    private static string? ClassifyCharacter(char ch)
    {
        // Latin: U+0041-007A, U+00C0-024F, U+1E00-1EFF
        if (ch is >= '\u0041' and <= '\u007A' ||
            ch is >= '\u00C0' and <= '\u024F' ||
            ch is >= '\u1E00' and <= '\u1EFF')
            return "latin";

        // CJK Unified Ideographs: U+4E00-9FFF, U+3400-4DBF
        // Hiragana: U+3040-309F, Katakana: U+30A0-30FF
        // Hangul: U+AC00-D7AF
        if (ch is >= '\u4E00' and <= '\u9FFF' ||
            ch is >= '\u3400' and <= '\u4DBF' ||
            ch is >= '\u3040' and <= '\u30FF' ||
            ch is >= '\uAC00' and <= '\uD7AF')
            return "cjk";

        // Arabic: U+0600-06FF
        if (ch is >= '\u0600' and <= '\u06FF')
            return "arabic";

        // Hebrew: U+0590-05FF
        if (ch is >= '\u0590' and <= '\u05FF')
            return "hebrew";

        // Greek: U+0370-03FF
        if (ch is >= '\u0370' and <= '\u03FF')
            return "greek";

        // Cyrillic: U+0400-04FF
        if (ch is >= '\u0400' and <= '\u04FF')
            return "cyrillic";

        // Thai: U+0E00-0E7F
        if (ch is >= '\u0E00' and <= '\u0E7F')
            return "thai";

        // Devanagari: U+0900-097F
        if (ch is >= '\u0900' and <= '\u097F')
            return "devanagari";

        return null;
    }

    private static string MapScriptToLanguage(string script) => script switch
    {
        "cjk" => "zh", // Default to Chinese for CJK
        "arabic" => "ar",
        "hebrew" => "he",
        "greek" => "el",
        "cyrillic" => "ru",
        "thai" => "th",
        "devanagari" => "hi",
        _ => "unknown"
    };

    /// <summary>
    /// Attempts to identify specific Western language from Latin text.
    /// Uses common word frequency analysis for top languages.
    /// </summary>
    private static string DetectSpecificLanguage(string text, string script)
    {
        if (string.Equals(script, "greek", StringComparison.Ordinal)) return "el";
        if (string.Equals(script, "cyrillic", StringComparison.Ordinal)) return "ru";

        // For Latin script, use common word detection
        var lowerText = text.ToLowerInvariant();
        var words = lowerText.Split(SplitChars,
            StringSplitOptions.RemoveEmptyEntries);

        var langScores = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (lang, markers) in LanguageMarkers)
        {
            var score = words.Count(w => markers.Contains(w));
            if (score > 0)
                langScores[lang] = score;
        }

        if (langScores.Count == 0)
            return "en"; // Default to English if no markers match

        return langScores.MaxBy(kv => kv.Value).Key;
    }

    /// <summary>
    /// Common function words per language for quick identification.
    /// These are high-frequency words unlikely to appear in other languages.
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> LanguageMarkers = new(StringComparer.Ordinal)
    {
        ["en"] = new(StringComparer.Ordinal) { "the", "and", "is", "in", "to", "of", "that", "it", "for", "you", "with", "each", "player", "game", "turn", "card" },
        ["it"] = new(StringComparer.Ordinal) { "il", "la", "di", "che", "un", "una", "del", "per", "sono", "giocatore", "gioco", "turno", "carta" },
        ["de"] = new(StringComparer.Ordinal) { "der", "die", "das", "und", "ist", "ein", "eine", "den", "dem", "spieler", "spiel", "zug", "karte" },
        ["fr"] = new(StringComparer.Ordinal) { "le", "la", "les", "des", "est", "une", "dans", "pour", "joueur", "jeu", "tour", "carte" },
        ["es"] = new(StringComparer.Ordinal) { "el", "los", "las", "del", "que", "una", "por", "con", "jugador", "juego", "turno", "carta" },
        ["pt"] = new(StringComparer.Ordinal) { "os", "das", "dos", "uma", "que", "pelo", "jogador", "jogo", "turno" },
        ["nl"] = new(StringComparer.Ordinal) { "het", "een", "van", "dat", "zijn", "speler", "spel", "beurt", "kaart" },
        ["pl"] = new(StringComparer.Ordinal) { "jest", "nie", "tak", "gracz", "gra", "tura", "karta" },
        ["sv"] = new(StringComparer.Ordinal) { "och", "att", "det", "som", "spelare", "spel", "tur", "kort" },
        ["da"] = new(StringComparer.Ordinal) { "og", "det", "som", "spiller", "spil", "tur", "kort" },
        ["no"] = new(StringComparer.Ordinal) { "og", "det", "som", "spiller", "spill", "tur", "kort" },
        ["fi"] = new(StringComparer.Ordinal) { "on", "ei", "pelaaja", "peli", "vuoro", "kortti" },
        ["cs"] = new(StringComparer.Ordinal) { "je", "jsou", "jako", "nebo", "ale", "hrac", "hra", "kolo", "karta" },
        ["hu"] = new(StringComparer.Ordinal) { "egy", "nem", "hogy", "van", "jatekos", "jatek", "kor", "kartya" },
        ["ro"] = new(StringComparer.Ordinal) { "este", "sunt", "jucator", "joc", "tura", "carte" },
        ["hr"] = new(StringComparer.Ordinal) { "je", "su", "kao", "igrac", "igra", "potez", "karta" }
    };
}

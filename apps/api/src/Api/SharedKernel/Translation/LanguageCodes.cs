namespace Api.SharedKernel.Translation;

/// <summary>
/// Shared language code dictionary and helpers for gamebook translation features.
/// Extracted from TranslateGamebookSegmentQueryHandler (DEC-14 #1559) per DEC-BE-10 (#1774).
/// </summary>
public static class LanguageCodes
{
    /// <summary>Maps ISO-639-1 uppercase codes to English language names for prompt construction.</summary>
    public static readonly IReadOnlyDictionary<string, string> LangNameByCode =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["EN"] = "English",
            ["FR"] = "French",
            ["DE"] = "German",
            ["ES"] = "Spanish",
            ["IT"] = "Italian",
        };

    /// <summary>Returns the English language name for the given code, or null if not supported.</summary>
    public static string? TryGetLanguageName(string code) =>
        LangNameByCode.TryGetValue(code, out var name) ? name : null;

    /// <summary>Returns true if the code is one of EN/FR/DE/ES/IT (case-insensitive).</summary>
    public static bool IsValidSourceLang(string? code) =>
        code is not null && LangNameByCode.ContainsKey(code);
}

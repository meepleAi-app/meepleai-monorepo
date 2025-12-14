using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// ISO 639-1 language code value object for PDF language identification.
/// Issue #2029: Validates language codes for PDF filtering
/// </summary>
public sealed class LanguageCode : ValueObject
{
    // Supported ISO 639-1 codes for MeepleAI
    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "en", // English
        "it", // Italian
        "de", // German
        "fr", // French
        "es", // Spanish
        "pt", // Portuguese
        "pl", // Polish
        "nl", // Dutch
        "ja", // Japanese
        "zh"  // Chinese
    };

    public string Value { get; }

    public LanguageCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ValidationException(nameof(LanguageCode), "Language code cannot be empty");

        var normalized = code.Trim().ToLowerInvariant();

        if (normalized.Length != 2)
            throw new ValidationException(nameof(LanguageCode), $"Language code must be exactly 2 characters (ISO 639-1), got: {code}");

        if (!SupportedLanguages.Contains(normalized))
            throw new ValidationException(nameof(LanguageCode),
                $"Language code must be one of: {string.Join(", ", SupportedLanguages.OrderBy(x => x, StringComparer.Ordinal))}. Got: {code}");

        Value = normalized;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value.ToLowerInvariant();
    }

    public override string ToString() => Value;

    // Convenience methods
    public static LanguageCode English => new("en");
    public static LanguageCode Italian => new("it");
    public static LanguageCode German => new("de");
    public static LanguageCode French => new("fr");
    public static LanguageCode Spanish => new("es");

    public static bool IsSupported(string code) =>
        !string.IsNullOrWhiteSpace(code) && SupportedLanguages.Contains(code.Trim().ToLowerInvariant());

    public static IReadOnlySet<string> GetSupportedLanguages() => SupportedLanguages;
}

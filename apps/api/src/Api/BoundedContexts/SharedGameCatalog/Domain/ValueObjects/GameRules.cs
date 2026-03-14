using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Value object representing game rules content with language specification.
/// Rules are stored as rich text (HTML/Markdown) for proper formatting.
/// Optionally includes an external URL to a rulebook PDF.
/// </summary>
public sealed class GameRules : ValueObject
{
    /// <summary>
    /// The rules content in rich text format (HTML or Markdown).
    /// </summary>
    public string Content { get; private set; }

    /// <summary>
    /// ISO 639-1 language code (e.g., "it", "en").
    /// </summary>
    public string Language { get; private set; }

    /// <summary>
    /// External URL to a rulebook PDF (e.g., from BGG).
    /// Must use HTTPS when set.
    /// </summary>
    public string? ExternalUrl { get; private set; }

    private GameRules(string content, string language, string? externalUrl = null)
    {
        Content = content;
        Language = language;
        ExternalUrl = externalUrl;
    }

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private GameRules()
    {
        Content = string.Empty;
        Language = string.Empty;
    }

    /// <summary>
    /// Creates a new GameRules value object with full content and optional external URL.
    /// </summary>
    public static GameRules Create(string content, string language, string? externalUrl = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Rules content cannot be empty", nameof(content));

        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Language cannot be empty", nameof(language));

        if (language.Length != 2)
            throw new ArgumentException("Language must be a valid ISO 639-1 code (2 characters)", nameof(language));

        if (externalUrl is not null)
            ValidateExternalUrl(externalUrl);

        return new GameRules(content, language, externalUrl);
    }

    /// <summary>
    /// Creates a GameRules with only an external URL (no inline content).
    /// Used during BGG enrichment when only a rulebook link is available.
    /// </summary>
    public static GameRules CreateFromUrl(string externalUrl)
    {
        if (string.IsNullOrWhiteSpace(externalUrl))
            throw new ArgumentException("External URL cannot be empty", nameof(externalUrl));

        ValidateExternalUrl(externalUrl);

        return new GameRules(string.Empty, string.Empty, externalUrl);
    }

    private static void ValidateExternalUrl(string externalUrl)
    {
        if (!Uri.TryCreate(externalUrl, UriKind.Absolute, out var uri))
            throw new ArgumentException("External URL must be a valid absolute URL", nameof(externalUrl));

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("External URL must use HTTPS", nameof(externalUrl));
    }

    /// <summary>
    /// Returns the equality components for value object comparison.
    /// </summary>
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Content;
        yield return Language;
        yield return ExternalUrl ?? string.Empty;
    }
}

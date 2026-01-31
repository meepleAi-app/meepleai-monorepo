using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Value object representing game rules content with language specification.
/// Rules are stored as rich text (HTML/Markdown) for proper formatting.
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
    /// Initializes a new instance of the <see cref="GameRules"/> class.
    /// </summary>
    /// <param name="content">The rules content</param>
    /// <param name="language">The language code</param>
    private GameRules(string content, string language)
    {
        Content = content;
        Language = language;
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
    /// Creates a new GameRules value object with validation.
    /// </summary>
    /// <param name="content">The rules content</param>
    /// <param name="language">The language code</param>
    /// <returns>A new GameRules instance</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails</exception>
    public static GameRules Create(string content, string language)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Rules content cannot be empty", nameof(content));

        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Language cannot be empty", nameof(language));

        if (language.Length != 2)
            throw new ArgumentException("Language must be a valid ISO 639-1 code (2 characters)", nameof(language));

        return new GameRules(content, language);
    }

    /// <summary>
    /// Returns the equality components for value object comparison.
    /// </summary>
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Content;
        yield return Language;
    }
}

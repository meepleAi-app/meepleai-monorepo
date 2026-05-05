using Api.BoundedContexts.AgentMemory.Domain.Enums;

namespace Api.BoundedContexts.AgentMemory.Domain.Models;

/// <summary>
/// Represents a game glossary term added to a game's memory.
/// </summary>
internal sealed class GlossaryEntry
{
    private GlossaryEntry() { } // Required for JSON deserialization

    public string Term { get; private set; } = string.Empty;
    public string Definition { get; private set; } = string.Empty;
    public string Language { get; private set; } = "en";
    public GlossaryEntrySource Source { get; private set; }
    public DateTime AddedAt { get; private set; }

    public static GlossaryEntry Create(
        string term,
        string definition,
        string language,
        GlossaryEntrySource source)
    {
        if (string.IsNullOrWhiteSpace(term))
            throw new ArgumentException("Term cannot be empty.", nameof(term));

        if (string.IsNullOrWhiteSpace(definition))
            throw new ArgumentException("Definition cannot be empty.", nameof(definition));

        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Language cannot be empty.", nameof(language));

        return new GlossaryEntry
        {
            Term = term.Trim(),
            Definition = definition.Trim(),
            Language = language.ToLowerInvariant(),
            Source = source,
            AddedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Restores from persistence with the original AddedAt timestamp.
    /// </summary>
    internal static GlossaryEntry Restore(
        string term,
        string definition,
        string language,
        GlossaryEntrySource source,
        DateTime addedAt)
    {
        return new GlossaryEntry
        {
            Term = term,
            Definition = definition,
            Language = language,
            Source = source,
            AddedAt = addedAt
        };
    }
}

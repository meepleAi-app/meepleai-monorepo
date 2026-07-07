namespace Api.BoundedContexts.SessionTracking.Domain.ValueObjects;

/// <summary>
/// A single provenance context for a glossary term: the <see cref="BookId"/> and,
/// optionally, the paragraph reference (<see cref="ParagraphRef"/>) where the term
/// appears, plus an optional context-specific <see cref="Definition"/>.
///
/// Part of the multi-context glossary model (issue #2638 / SI-7): a term may appear
/// in several books/paragraphs, each carrying its own optional definition. The old
/// single-context pointer (<c>GamebookGlossaryEntry.FirstSeenBookId</c>) is retained
/// for backward-compat and still marks the "first" context.
///
/// The positional constructor is public so System.Text.Json can round-trip the value
/// from the JSONB payload; use <see cref="Create"/> for validated, normalized instances.
/// </summary>
public sealed record GlossaryContext(Guid BookId, string? ParagraphRef, string? Definition)
{
    /// <summary>
    /// Creates a normalized context. Throws when <paramref name="bookId"/> is empty.
    /// Whitespace-only <paramref name="paragraphRef"/> / <paramref name="definition"/>
    /// are normalized to <c>null</c>; otherwise they are trimmed.
    /// </summary>
    public static GlossaryContext Create(Guid bookId, string? paragraphRef = null, string? definition = null)
    {
        if (bookId == Guid.Empty)
            throw new ArgumentException("bookId required", nameof(bookId));

        return new GlossaryContext(bookId, Normalize(paragraphRef), Normalize(definition));
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

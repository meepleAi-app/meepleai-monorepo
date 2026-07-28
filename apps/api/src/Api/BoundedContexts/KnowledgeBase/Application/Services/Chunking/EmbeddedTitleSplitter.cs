using System.Collections.Frozen;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;

/// <summary>
/// Epic #3338 WP1a: a pre-pass over the raw <see cref="ExtractedElement"/> stream that recovers
/// section titles <c>unstructured</c>'s 'fast' strategy failed to emit as standalone <c>Title</c>
/// elements on complex multi-column layouts. When a body element's text contains an embedded ALL-CAPS
/// heading from <see cref="SectionHeadingLexicon"/> (the Terraforming Mars case:
/// <c>"…parentesi. 6 PREPARAZIONE Di seguito viene descritta…"</c> glued into a body element headed
/// "CARTE"), it is split into up to three elements — <c>[head body][Title(token)][tail body]</c> — so
/// the downstream <see cref="ExtractedDocumentFactory.GroupByTitle"/> opens a real section and the
/// #3270 heading-match boost + the RoleClassifier Setup fast-path stop being starved.
/// <para>
/// Runs at the ELEMENT layer (before <see cref="ExtractedDocumentFactory.FromExtraction"/>), so it
/// needs no character-offset recompute — the factory derives offsets from its own StringBuilder.
/// Detection is deliberately conservative to protect the well-extracted English corpus: an entry is
/// promoted only when it is (i) an exact UPPERCASE, whole-word match, (ii) preceded (ignoring spaces)
/// by a heading boundary — a digit, sentence terminator, or line break — and (iii) followed within a
/// short window by lowercase running prose. At most one split per element, non-recursive.
/// </para>
/// </summary>
internal static class EmbeddedTitleSplitter
{
    private const string TitleCategory = "Title";

    /// <summary>Characters after the token scanned for the lowercase-prose confirmation (guard iii).</summary>
    private const int TrailingProseWindow = 60;

    /// <summary>
    /// Only prose element types are split. Restricting to these (rather than merely excluding "Title")
    /// prevents tearing a Table row or a ListItem across a synthetic heading — the split target is
    /// running body prose, where unstructured glues a missed section title. <see cref="ExtractedElement"/>
    /// coalesces null/blank categories to "NarrativeText".
    /// </summary>
    private static readonly FrozenSet<string> SplittableTypes =
        new[] { "NarrativeText", "UncategorizedText" }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlyList<ExtractedElement> Split(IReadOnlyList<ExtractedElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        var result = new List<ExtractedElement>(elements.Count);
        foreach (var el in elements)
        {
            if (IsSplittable(el) && TryFindEmbeddedTitle(el.Text, out var start, out var length))
            {
                var head = el.Text[..start].TrimEnd();
                var token = el.Text.Substring(start, length);
                var tail = el.Text[(start + length)..].TrimStart();

                if (head.Length > 0)
                {
                    result.Add(el with { Text = head });
                }
                result.Add(new ExtractedElement(token, el.PageNumber, TitleCategory));
                if (tail.Length > 0)
                {
                    result.Add(el with { Text = tail });
                }
            }
            else
            {
                result.Add(el);
            }
        }

        return result;
    }

    private static bool IsSplittable(ExtractedElement el) =>
        !string.IsNullOrEmpty(el.Text)
        && SplittableTypes.Contains(el.ElementType);

    /// <summary>
    /// Finds the LEFTMOST qualifying lexicon title embedded in <paramref name="text"/> (longest entry
    /// wins on an equal start, so "GAME SETUP" beats "SETUP" at the same position). Returns false when
    /// no occurrence satisfies the heading guards.
    /// </summary>
    private static bool TryFindEmbeddedTitle(string text, out int start, out int length)
    {
        start = -1;
        length = 0;

        foreach (var title in SectionHeadingLexicon.Titles)
        {
            var from = 0;
            while (from <= text.Length - title.Length)
            {
                var i = text.IndexOf(title, from, StringComparison.Ordinal);
                if (i < 0)
                {
                    break;
                }

                if (QualifiesAsHeading(text, i, title.Length)
                    && (start < 0 || i < start || (i == start && title.Length > length)))
                {
                    start = i;
                    length = title.Length;
                }

                from = i + 1;
            }
        }

        return start >= 0;
    }

    private static bool QualifiesAsHeading(string text, int i, int len)
    {
        var end = i + len;

        // (i) whole-word: not glued to a letter on either side ("PREPARAZIONE" not "PREPARAZIONER",
        //     and not the tail of "IMPREPARAZIONE").
        if (i > 0 && char.IsLetter(text[i - 1]))
        {
            return false;
        }
        if (end < text.Length && char.IsLetter(text[end]))
        {
            return false;
        }

        // (ii) the token must sit at a STRONG heading boundary: the start of the element, a line break,
        //      the end of the previous sentence (. ! ?), or a page-number token — a digit run isolated
        //      at a clause boundary, the TM "…parentesi. 6 PREPARAZIONE" pattern. A bare preceding digit
        //      is NOT enough: a prose quantifier ("esegui 2 AZIONI a tua scelta") would otherwise
        //      fabricate a bogus "AZIONI" heading corpus-wide. Colons/parens/commas/letters are inline
        //      and rejected.
        var p = i - 1;
        while (p >= 0 && (text[p] == ' ' || text[p] == '\t'))
        {
            p--;
        }
        if (p >= 0 && !IsStrongBoundary(text[p]))
        {
            if (!char.IsDigit(text[p]))
            {
                return false;
            }

            // Preceding char is a digit: qualify ONLY if the whole digit run is itself at a clause
            // boundary (a page number), not a quantifier embedded after a word.
            while (p >= 0 && char.IsDigit(text[p]))
            {
                p--;
            }
            while (p >= 0 && (text[p] == ' ' || text[p] == '\t'))
            {
                p--;
            }
            if (p >= 0 && !IsStrongBoundary(text[p]))
            {
                return false;
            }
        }

        // (iii) followed within a short window by lowercase running prose — confirms a heading followed
        //       by body text, not a stray ALL-CAPS word or a run of adjacent all-caps headings.
        var windowEnd = Math.Min(text.Length, end + TrailingProseWindow);
        for (var q = end; q < windowEnd; q++)
        {
            if (char.IsLower(text[q]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A line break or a sentence terminator — the marks that end the previous section's text.</summary>
    private static bool IsStrongBoundary(char c) =>
        c == '\n' || c == '\r' || c == '.' || c == '!' || c == '?';
}

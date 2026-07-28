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
/// Also promotes a standalone <c>Header</c> element to <c>Title</c> when its text carries a curated
/// section word (the TM "PREPARAZIONE" case: unstructured emitted it as a <c>Header</c>, which
/// <see cref="ExtractedDocumentFactory.GroupByTitle"/> ignores, so it was absorbed into the previous
/// section's chunk). Gated on the lexicon because <c>Header</c> is dominantly running page-header noise.
/// </para>
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
    private const string HeaderCategory = "Header";

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

    /// <summary>
    /// Element types that constitute section BODY. A promoted Header must be immediately followed by one
    /// of these, otherwise it is a bodyless running-header and would yield a heading-only junk chunk.
    /// </summary>
    private static readonly FrozenSet<string> ContentTypes =
        new[] { "NarrativeText", "UncategorizedText", "ListItem", "Table" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>A clean section-title Header is short — longer runs are running-header/footer noise.</summary>
    private const int MaxPromotableHeaderLength = 60;

    public static IReadOnlyList<ExtractedElement> Split(IReadOnlyList<ExtractedElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        var result = new List<ExtractedElement>(elements.Count);
        for (var idx = 0; idx < elements.Count; idx++)
        {
            var el = elements[idx];
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
            else if (IsPromotableHeader(el, idx + 1 < elements.Count ? elements[idx + 1] : null))
            {
                // A section title unstructured emitted as a standalone "Header" (not "Title") — the TM
                // "PREPARAZIONE" case. GroupByTitle opens sections only on "Title", so re-tag the WHOLE
                // element as Title. See IsPromotableHeader for the guards that keep running-header/footer
                // noise (which dominates "Header" corpus-wide, up to 825/doc) from fabricating sections.
                result.Add(el with { ElementType = TitleCategory });
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
    /// True when the element is a <c>Header</c> that is a real section title unstructured miscategorised
    /// as a running header — re-tagged wholesale to <c>Title</c> by the caller (a Header IS the heading,
    /// so unlike the split path there is no head/tail). Because "Header" is dominantly running
    /// page-header/footer noise, promotion requires ALL of: (1) it is a short (≤60 char) all-caps title
    /// — no lowercase or digits, which rejects page refs / filenames / "SETUP p.6" / "London"; (2) it
    /// carries a curated <see cref="SectionHeadingLexicon"/> section word (whole-word), so a bare
    /// all-caps banner like the game title "TERRAFORMING MARS" is not promoted; (3) it is immediately
    /// followed by a body element, so a bodyless running header does not spawn a heading-only junk chunk.
    /// </summary>
    private static bool IsPromotableHeader(ExtractedElement el, ExtractedElement? next)
    {
        if (!string.Equals(el.ElementType, HeaderCategory, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(el.Text))
        {
            return false;
        }

        var text = el.Text.Trim();
        if (text.Length > MaxPromotableHeaderLength || !IsAllCapsTitle(text))
        {
            return false;
        }

        if (next is null || !ContentTypes.Contains(next.ElementType))
        {
            return false;
        }

        return ContainsLexiconTitle(text);
    }

    /// <summary>True when the text is an ALL-CAPS title: no lowercase letters and no digits (spaces and
    /// title punctuation allowed). Running-header/footer noise carries page numbers or lowercase.</summary>
    private static bool IsAllCapsTitle(string text)
    {
        var hasLetter = false;
        foreach (var ch in text)
        {
            if (char.IsLower(ch) || char.IsDigit(ch))
            {
                return false;
            }
            if (char.IsLetter(ch))
            {
                hasLetter = true;
            }
        }
        return hasLetter;
    }

    /// <summary>
    /// True when <paramref name="text"/> contains a curated <see cref="SectionHeadingLexicon"/> section
    /// title as an exact UPPERCASE whole word. Shared with <see cref="TitleHealthMetric"/> (WP3 canonical
    /// coverage) so the "is this a known section type" predicate has a single definition.
    /// </summary>
    internal static bool ContainsLexiconTitle(string text)
    {
        foreach (var title in SectionHeadingLexicon.Titles)
        {
            var i = text.IndexOf(title, StringComparison.Ordinal);
            while (i >= 0)
            {
                if (IsWholeWordAt(text, i, title.Length))
                {
                    return true;
                }
                i = text.IndexOf(title, i + 1, StringComparison.Ordinal);
            }
        }
        return false;
    }

    /// <summary>The match at [<paramref name="i"/>, i+<paramref name="len"/>) is not glued to a letter on
    /// either side. Shared by the split path (guard i) and the Header-promotion path.</summary>
    private static bool IsWholeWordAt(string text, int i, int len)
    {
        if (i > 0 && char.IsLetter(text[i - 1]))
        {
            return false;
        }
        var end = i + len;
        return end >= text.Length || !char.IsLetter(text[end]);
    }

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
        if (!IsWholeWordAt(text, i, len))
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

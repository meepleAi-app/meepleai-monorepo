using System.Text.Json;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// Where one page of the PDF begins in the extracted text (#4782): <paramref name="Start"/> is a character offset in
/// <c>ExtractedText</c>, <paramref name="Page"/> the 1-based page whose text starts there. A page runs until the next
/// span (minus the separator the pipelines put between pages) or the end of the text.
/// </summary>
public sealed record PageSpan(int Page, int Start);

/// <summary>
/// The page map of an extracted text: how it is checked against the text it describes, and how it is stored.
/// </summary>
/// <remarks>
/// The extraction pipelines join the non-empty pages of a PDF with <see cref="Separator"/> into <c>ExtractedText</c>,
/// and a later re-index rebuilds the chunks from that text alone. Without the map the page of every passage is lost
/// at that point, so a re-index would turn real pages back into «page 1». The map is therefore kept next to the text
/// (<c>pdf_documents.page_spans_json</c>) and re-checked against it on use: a map that no longer fits the text is
/// worse than none, because it would put passages on the wrong page with the look of a measured one.
/// </remarks>
public static class PageSpans
{
    /// <summary>What the pipelines put between two pages when they join them into <c>ExtractedText</c>.</summary>
    public const string Separator = "\n\n";

    /// <summary>
    /// True when <paramref name="spans"/> describes <paramref name="text"/>: the first page starts at 0, pages are 1 or
    /// more and never go back, every page holds at least one character, and each page after the first is preceded by
    /// the separator. A text edited after the map was written fails this, and so does a map from another text.
    /// </summary>
    public static bool IsConsistentWith(IReadOnlyList<PageSpan>? spans, string? text)
    {
        if (spans is null || spans.Count == 0 || string.IsNullOrEmpty(text) || spans[0].Start != 0)
        {
            return false;
        }

        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            // A negative start must be refused here: below it would wrap around in the arithmetic and then
            // throw instead of answering false.
            if (span.Page < 1 || span.Start < 0 || span.Start >= text.Length)
            {
                return false;
            }

            if (i == 0)
            {
                continue;
            }

            var previous = spans[i - 1];
            var separatorStart = span.Start - Separator.Length;
            if (span.Page < previous.Page
                || separatorStart <= previous.Start
                || !text.AsSpan(separatorStart, Separator.Length).SequenceEqual(Separator))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The page that holds the character at <paramref name="offset"/>: that of the last span starting at or before it.
    /// An offset inside the separator between two pages belongs to the page before. The map must be consistent with the
    /// text (<see cref="IsConsistentWith"/>); an offset before the first span gets the first page.
    /// </summary>
    public static int PageAt(IReadOnlyList<PageSpan> spans, int offset)
    {
        ArgumentNullException.ThrowIfNull(spans);

        var found = 0;
        var low = 0;
        var high = spans.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (spans[middle].Start <= offset)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return spans[found].Page;
    }

    /// <summary>Stores the map as <c>[[page,start],…]</c>: a page per pair, in text order.</summary>
    public static string Serialize(IReadOnlyList<PageSpan> spans)
    {
        ArgumentNullException.ThrowIfNull(spans);
        return JsonSerializer.Serialize(spans.Select(s => new[] { s.Page, s.Start }));
    }

    /// <summary>
    /// Reads what <see cref="Serialize"/> wrote. Null for no value and for anything that is not a list of pairs: the
    /// caller then has no page map, which is a legitimate state (every document extracted before #4782 is in it).
    /// </summary>
    public static IReadOnlyList<PageSpan>? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var pairs = JsonSerializer.Deserialize<int[][]>(json);
            if (pairs is null || pairs.Any(p => p is null || p.Length != 2))
            {
                return null;
            }

            return pairs.Select(p => new PageSpan(p[0], p[1])).ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

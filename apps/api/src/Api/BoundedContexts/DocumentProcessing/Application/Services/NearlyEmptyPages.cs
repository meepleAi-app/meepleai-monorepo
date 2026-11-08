using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// What an extraction says about the pages that carry (almost) no text: scans, full-page art, blank separators (#4437).
/// Docnet reads the text layer and does not OCR, so a scanned page comes back empty; a document with a few of them still
/// reaches <c>Ready</c>, with holes nobody was told about. This is what makes the holes visible.
/// </summary>
internal static class NearlyEmptyPages
{
    /// <summary>
    /// A page with fewer non-blank characters than this counts as nearly empty. It is the threshold
    /// <c>ShouldTriggerOcr</c> used to decide that a page «looks like a scan» (the OCR fallback it served is gone), so the
    /// words mean what they meant.
    /// </summary>
    public const int MaxCharacters = 100;

    /// <summary>
    /// How many of the pages are nearly empty. Zero when the pages are not the pages of the PDF
    /// (<see cref="PageTextChunk.PageIsEstimated"/>, the Unstructured extractor cuts the whole text into equal parts):
    /// counting short slices of a text says nothing about the document.
    /// </summary>
    public static int Count(IEnumerable<PageTextChunk> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        var list = pages as IReadOnlyCollection<PageTextChunk> ?? pages.ToList();
        if (list.Any(page => page.PageIsEstimated))
        {
            return 0;
        }

        return list.Count(page => (page.Text ?? string.Empty).Trim().Length < MaxCharacters);
    }

    /// <summary>
    /// True when the extraction found pages and not one of them has text: the document is, as far as this extractor can
    /// tell, a scan.
    /// </summary>
    public static bool NoPageHasText(IEnumerable<PageTextChunk> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        var list = pages as IReadOnlyCollection<PageTextChunk> ?? pages.ToList();
        return list.Count > 0 && list.All(page => page.IsEmpty);
    }
}

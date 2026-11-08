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
    /// A page with fewer characters than this, once the whitespace at both ends is trimmed (spaces inside the text do
    /// count), is nearly empty. 100 is also the default of the key the removed <c>ShouldTriggerOcr</c> compared against
    /// (<c>PdfExtraction:Ocr:ThresholdCharsPerPage</c>), but that rule compared the AVERAGE over the whole document; here it
    /// is applied to each page, so a single cover or blank page counts.
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
    /// How many pages of this extraction are nearly empty, or zero when the extractor is not text-layer-only
    /// (<see cref="PagedTextExtractionResult.TextLayerOnly"/>): for an extractor that reads pixels, or one that does not
    /// know its pages, a short page says nothing about a scan.
    /// </summary>
    public static int CountIn(PagedTextExtractionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.TextLayerOnly ? Count(result.PageChunks) : 0;
    }

    /// <summary>
    /// True when the extractor reads the text layer alone and found pages, none with text: a scan, as far as it can tell.
    /// False for every other extractor, so the old, generic failure stays as it was for them.
    /// </summary>
    public static bool NoTextAnywhere(PagedTextExtractionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.TextLayerOnly && NoPageHasText(result.PageChunks);
    }

    /// <summary>
    /// True when the extraction found pages and not one of them has text. It does not look at who extracted:
    /// <see cref="NoTextAnywhere"/> does.
    /// </summary>
    public static bool NoPageHasText(IEnumerable<PageTextChunk> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        var list = pages as IReadOnlyCollection<PageTextChunk> ?? pages.ToList();
        return list.Count > 0 && list.All(page => page.IsEmpty);
    }
}

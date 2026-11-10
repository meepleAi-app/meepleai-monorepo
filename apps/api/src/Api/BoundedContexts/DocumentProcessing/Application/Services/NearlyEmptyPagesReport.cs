using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Observability;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Where the nearly-empty pages of an extraction are made visible (#4437, #4931): one place for the two pipelines that read
/// the text layer, so the Warning and the counter cannot drift apart. The rule is <see cref="NearlyEmptyPages.Assess"/>.
/// </summary>
internal static class NearlyEmptyPagesReport
{
    /// <summary>
    /// Counts the document in <c>meepleai.pdf.extraction.nearly_empty</c> and logs it: Warning when the rule flags it, Debug
    /// when only a few pages are nearly empty (usually a cover), nothing
    /// when none is. Does nothing for an extractor that does not read the text layer alone: there a short page says nothing
    /// about a scan, and counting its documents as «clean» would be a lie.
    /// </summary>
    public static void Record(ILogger logger, string pdfId, PagedTextExtractionResult extraction)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(extraction);

        if (!extraction.TextLayerOnly)
        {
            return;
        }

        var assessment = NearlyEmptyPages.Assess(extraction);
        var outcome = assessment.Flagged
            ? MeepleAiMetrics.NearlyEmptyFlagged
            : assessment.NearlyEmpty > 0 ? MeepleAiMetrics.NearlyEmptyPartial : MeepleAiMetrics.NearlyEmptyClean;
        MeepleAiMetrics.PdfNearlyEmpty.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

        if (assessment.Flagged)
        {
            logger.LogWarning(
                "PDF {PdfId}: {NearlyEmptyPages} of {TotalPages} pages have fewer than {MaxCharacters} characters of text " +
                "(flagged from {FlagFraction:P0} of the pages, at least {FlagMinPages}); if they are scans, this extraction " +
                "does not OCR them and they are missing from the index",
                pdfId, assessment.NearlyEmpty, assessment.TotalPages, NearlyEmptyPages.MaxCharacters,
                NearlyEmptyPages.FlagFractionOfPages, NearlyEmptyPages.FlagMinPages);
        }
        else if (assessment.NearlyEmpty > 0)
        {
            logger.LogDebug(
                "PDF {PdfId}: {NearlyEmptyPages} of {TotalPages} pages have fewer than {MaxCharacters} characters of text " +
                "(below the flag rule, usually a cover)",
                pdfId, assessment.NearlyEmpty, assessment.TotalPages, NearlyEmptyPages.MaxCharacters);
        }
    }
}

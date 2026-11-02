using System.Text;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>The extracted text of a PDF, and where each of its pages begins when that is known.</summary>
/// <param name="Text">The non-empty pages joined with <see cref="PageSpans.Separator"/>: what goes in <c>ExtractedText</c>.</param>
/// <param name="Spans">
/// Where each page starts in <paramref name="Text"/>, or null when the extractor did not measure its pages (see
/// <see cref="PageTextChunk.PageIsEstimated"/>): a page map that is a guess is no better than «page 1».
/// </param>
internal sealed record JoinedPageText(string Text, IReadOnlyList<PageSpan>? Spans);

/// <summary>
/// The one place that turns the pages of an extraction into <c>ExtractedText</c> (#4782). The three pipelines used to
/// join them by hand (<c>string.Join("\n\n", …)</c>) and threw the page boundaries away; building the text and its page
/// map in the same loop is what keeps them describing the same string.
/// </summary>
internal static class PageTextJoiner
{
    public static JoinedPageText Join(IEnumerable<PageTextChunk> pageChunks)
    {
        ArgumentNullException.ThrowIfNull(pageChunks);

        var text = new StringBuilder();
        var spans = new List<PageSpan>();
        var measured = true;

        foreach (var chunk in pageChunks.Where(pc => !pc.IsEmpty))
        {
            if (spans.Count > 0)
            {
                text.Append(PageSpans.Separator);
                // A page that goes back in the text order does not describe a document laid out page by page.
                measured &= chunk.PageNumber >= spans[^1].Page;
            }

            measured &= !chunk.PageIsEstimated && chunk.PageNumber >= 1;
            spans.Add(new PageSpan(chunk.PageNumber, text.Length));
            text.Append(chunk.Text);
        }

        return new JoinedPageText(text.ToString(), measured && spans.Count > 0 ? spans : null);
    }
}

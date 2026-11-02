using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// #4782 — the behaviour the issue asks for: a multi-page document that reaches the chunker as flat text (the
/// extractor gave no structured elements) produces chunks on distinct, true pages. Real chunker, no mocks: the unit
/// tests of the pieces cannot show that the page survives the whole way down to the chunk.
/// </summary>
/// <remarks>
/// A chunk is on the page where its text STARTS (as a structured section is on the page of its first element), and the
/// chunks are cut exactly as they were before the page map existed. So the oracle here is the page that holds the first
/// non-blank character of each chunk, computed from the lengths of the pages and not from the page map under test.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class FlatDocumentPagesTests
{
    private const int Pages = 12;

    private static readonly Guid DocumentId = Guid.NewGuid();
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AdvancedChunkingService _chunker = new(
        new TextChunkingService(NullLogger<TextChunkingService>.Instance),
        new ChunkingStrategySelector(),
        NullLogger<AdvancedChunkingService>.Instance);

    // About 3,400 characters a page: longer than a chunk, as in a real rulebook, so every page has chunks starting on it.
    private static PageTextChunk[] RulebookPages() =>
        Enumerable.Range(1, Pages)
            .Select(n =>
            {
                var text = string.Join(' ', Enumerable.Range(1, 40).Select(i =>
                    $"On page {n} the players resolve effect number {i} before the next round begins."));
                return new PageTextChunk(n, text, 0, text.Length);
            })
            .ToArray();

    /// <summary>Where each non-empty page begins in the joined text, from the lengths of the pages alone.</summary>
    private static List<(int Page, int Start)> TruePageStarts(IEnumerable<PageTextChunk> pages)
    {
        var starts = new List<(int Page, int Start)>();
        var offset = 0;
        foreach (var page in pages.Where(p => !p.IsEmpty))
        {
            starts.Add((page.PageNumber, offset));
            offset += page.Text.Length + 2;
        }

        return starts;
    }

    private static int TruePageOf(DocumentChunk chunk, string text, List<(int Page, int Start)> starts)
    {
        var first = chunk.CharStart;
        while (char.IsWhiteSpace(text[first]))
        {
            first++;
        }

        return starts.Last(s => s.Start <= first).Page;
    }

    private Task<List<DocumentChunk>> BuildAsync(string text, IReadOnlyList<PageSpan>? spans) =>
        HeadingAwareChunker.BuildAsync(
            structuredElements: null, text, DocumentId, gameId: null, _chunker, Token, pageSpans: spans);

    [Fact]
    public async Task AFlatDocumentWithMeasuredPages_ProducesChunksOnTheirTruePages()
    {
        var pages = RulebookPages();
        var joined = PageTextJoiner.Join(pages);
        var starts = TruePageStarts(pages);

        var chunks = await BuildAsync(joined.Text, joined.Spans);

        chunks.Should().NotBeEmpty();
        chunks.Select(c => c.Page).Distinct().Should().BeEquivalentTo(Enumerable.Range(1, Pages));
        chunks.Should().OnlyContain(c => c.Page == TruePageOf(c, joined.Text, starts),
            "each chunk must sit on the page where its text starts");
    }

    [Fact]
    public async Task ThePageMapChangesOnlyThePageOfEachChunk_NotHowTheTextIsCut()
    {
        // The retrieval gates were measured on the way a flat document is cut: parents, children, offsets. A page
        // map that moved those would move the index with it. It must only put the right page on each chunk.
        var joined = PageTextJoiner.Join(RulebookPages());

        var withMap = await BuildAsync(joined.Text, joined.Spans);
        var withoutMap = await BuildAsync(joined.Text, spans: null);

        withMap.Should().NotBeEmpty();
        withMap.Select(c => (c.Text, c.CharStart, c.CharEnd, c.Level, c.Heading, c.ElementType))
            .Should().Equal(withoutMap.Select(c => (c.Text, c.CharStart, c.CharEnd, c.Level, c.Heading, c.ElementType)));
    }

    [Fact]
    public async Task TheSameDocumentWithoutAPageMap_IsStillOnePageOne_TheDefectThisFixes()
    {
        var joined = PageTextJoiner.Join(RulebookPages());

        var chunks = await BuildAsync(joined.Text, spans: null);

        chunks.Select(c => c.Page).Distinct().Should().Equal(1);
    }

    [Fact]
    public async Task ThePageMapSurvivesBeingStoredAndReadBack_SoAReindexKeepsThePages()
    {
        var pages = RulebookPages();
        var joined = PageTextJoiner.Join(pages);
        var starts = TruePageStarts(pages);
        var stored = PageSpans.Serialize(joined.Spans!);

        var chunks = await BuildAsync(joined.Text, PageSpans.Parse(stored));

        chunks.Should().OnlyContain(c => c.Page == TruePageOf(c, joined.Text, starts));
        chunks.Select(c => c.Page).Distinct().Should().HaveCount(Pages);
    }

    [Fact]
    public async Task APageMapThatNoLongerFitsTheText_FallsBackToPageOne_NotToWrongPages()
    {
        var joined = PageTextJoiner.Join(RulebookPages());
        var staleSpans = joined.Spans!.Select(s => new PageSpan(s.Page, s.Start + 1)).ToList();

        var chunks = await BuildAsync(joined.Text, staleSpans);

        chunks.Select(c => c.Page).Distinct().Should().Equal(1);
    }

    // Sentences and pages of uneven lengths slide the page boundaries across the steps of the chunker. The unevenness
    // changes the layout; the layouts are deterministic.
    private static PageTextChunk[] PagesOfUnevenLength(int unevenness) =>
        Enumerable.Range(1, 40)
            .Select(n =>
            {
                var text = string.Join(' ', Enumerable.Range(1, 30 + (n * 7 % 23)).Select(i =>
                    $"Page {n} rule {i} resolves {new string('x', (i * 5 + n) % unevenness)} first."));
                return new PageTextChunk(n, text, 0, text.Length);
            })
            .ToArray();

    [Fact]
    public async Task AChunkThatBeginsBeforeAPageBreakButHasItsWordsAfterIt_IsOnThePageOfItsWords()
    {
        // TextChunkingService trims the text of a chunk but reports where the span began BEFORE trimming: a chunk
        // that begins on the blank line between two pages has a CharStart on the previous page and its words on the
        // next one. Found on a real rulebook, where a chunk whose first words were on page 6 was given page 5.
        // It is rare (the start of a chunk has to land on the two characters of the blank line), so the test looks
        // for layouts that produce it, checks every chunk of every layout, and requires at least one such chunk.
        var straddling = 0;
        foreach (var unevenness in Enumerable.Range(3, 27))
        {
            var pages = PagesOfUnevenLength(unevenness);
            var joined = PageTextJoiner.Join(pages);
            var starts = TruePageStarts(pages);

            var chunks = await BuildAsync(joined.Text, joined.Spans);

            straddling += chunks.Count(c => starts.Last(s => s.Start <= c.CharStart).Page != TruePageOf(c, joined.Text, starts));
            chunks.Should().OnlyContain(c => c.Page == TruePageOf(c, joined.Text, starts), "unevenness {0}", unevenness);
        }

        straddling.Should().BeGreaterThan(0, "no layout made a chunk begin before a page break, so this test proves nothing");
    }

    [Fact]
    public async Task APageWithoutText_LeavesAGapInThePages_AndNoChunkIsPlacedOnIt()
    {
        PageTextChunk[] pages = [.. RulebookPages().Take(2), new PageTextChunk(3, "  ", 0, 2), .. RulebookPages().Skip(3).Take(2)];
        var joined = PageTextJoiner.Join(pages);
        var starts = TruePageStarts(pages);

        var chunks = await BuildAsync(joined.Text, joined.Spans);

        chunks.Select(c => c.Page).Distinct().Should().BeEquivalentTo(new[] { 1, 2, 4, 5 });
        chunks.Should().OnlyContain(c => c.Page == TruePageOf(c, joined.Text, starts));
    }
}

using System.Text.RegularExpressions;
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
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed partial class FlatDocumentPagesTests
{
    private const int Pages = 12;

    private static readonly Guid DocumentId = Guid.NewGuid();
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AdvancedChunkingService _chunker = new(
        new TextChunkingService(NullLogger<TextChunkingService>.Instance),
        new ChunkingStrategySelector(),
        NullLogger<AdvancedChunkingService>.Instance);

    [GeneratedRegex(@"page (\d+)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PageMarker();

    // Every sentence of page N says "page N", so a chunk's own text tells which page it really came from.
    private static PageTextChunk[] RulebookPages() =>
        Enumerable.Range(1, Pages)
            .Select(n =>
            {
                var text = string.Join(' ', Enumerable.Range(1, 14).Select(i =>
                    $"On page {n} the players resolve effect number {i} before the next round begins."));
                return new PageTextChunk(n, text, 0, text.Length);
            })
            .ToArray();

    private static int PageClaimedByText(string text) => int.Parse(PageMarker().Match(text).Groups[1].Value);

    [Fact]
    public async Task AFlatDocumentWithMeasuredPages_ProducesChunksOnTheirTruePages()
    {
        var joined = PageTextJoiner.Join(RulebookPages());

        var chunks = await HeadingAwareChunker.BuildAsync(
            structuredElements: null, joined.Text, DocumentId, gameId: null, _chunker, Token, pageSpans: joined.Spans);

        chunks.Should().NotBeEmpty();
        chunks.Select(c => c.Page).Distinct().Should().BeEquivalentTo(Enumerable.Range(1, Pages));
        chunks.Should().OnlyContain(c => c.Page == PageClaimedByText(c.Text),
            "each chunk must sit on the page its own text comes from");
    }

    [Fact]
    public async Task TheSameDocumentWithoutAPageMap_IsStillOnePageOne_TheDefectThisFixes()
    {
        var joined = PageTextJoiner.Join(RulebookPages());

        var chunks = await HeadingAwareChunker.BuildAsync(
            structuredElements: null, joined.Text, DocumentId, gameId: null, _chunker, Token);

        chunks.Select(c => c.Page).Distinct().Should().Equal(1);
    }

    [Fact]
    public async Task ThePageMapSurvivesBeingStoredAndReadBack_SoAReindexKeepsThePages()
    {
        var joined = PageTextJoiner.Join(RulebookPages());
        var stored = PageSpans.Serialize(joined.Spans!);

        var chunks = await HeadingAwareChunker.BuildAsync(
            structuredElements: null, joined.Text, DocumentId, gameId: null, _chunker, Token,
            pageSpans: PageSpans.Parse(stored));

        chunks.Should().OnlyContain(c => c.Page == PageClaimedByText(c.Text));
        chunks.Select(c => c.Page).Distinct().Should().HaveCount(Pages);
    }

    [Fact]
    public async Task APageMapThatNoLongerFitsTheText_FallsBackToPageOne_NotToWrongPages()
    {
        var joined = PageTextJoiner.Join(RulebookPages());
        var staleSpans = joined.Spans!.Select(s => new PageSpan(s.Page, s.Start + 1)).ToList();

        var chunks = await HeadingAwareChunker.BuildAsync(
            structuredElements: null, joined.Text, DocumentId, gameId: null, _chunker, Token, pageSpans: staleSpans);

        chunks.Select(c => c.Page).Distinct().Should().Equal(1);
    }

    [Fact]
    public async Task APageWithoutText_LeavesAGapInThePages_AndNoChunkIsPlacedOnIt()
    {
        PageTextChunk[] pages = [.. RulebookPages().Take(2), new PageTextChunk(3, "  ", 0, 2), .. RulebookPages().Skip(3).Take(2)];
        var joined = PageTextJoiner.Join(pages);

        var chunks = await HeadingAwareChunker.BuildAsync(
            structuredElements: null, joined.Text, DocumentId, gameId: null, _chunker, Token, pageSpans: joined.Spans);

        chunks.Select(c => c.Page).Distinct().Should().BeEquivalentTo(new[] { 1, 2, 4, 5 });
        chunks.Should().OnlyContain(c => c.Page == PageClaimedByText(c.Text));
    }
}

using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Unit tests for <see cref="PageTextChunker"/>.
/// Libro Game AI Assistant MVP Phase 2 — Task 2.3a.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class PageTextChunkerTests
{
    private static readonly Guid BatchId = Guid.NewGuid();
    private static readonly Guid PageId = Guid.NewGuid();
    private const int PageNumber = 1;
    private const string Language = "en";
    private const float Confidence = 0.9f;

    private static PageTextChunker CreateSut() => new();

    [Fact]
    public void ChunkPage_ShortText_ReturnsSingleChunk()
    {
        // ARRANGE
        var sut = CreateSut();
        const string text = "Short text.";

        // ACT
        var chunks = sut.ChunkPage(BatchId, PageId, PageNumber, text, Language, Confidence);

        // ASSERT
        chunks.Should().HaveCount(1);
        chunks[0].TextContent.Should().Be(text);
        chunks[0].ChunkIndex.Should().Be(0);
        chunks[0].PageNumber.Should().Be(PageNumber);
        chunks[0].Language.Should().Be(Language);
        chunks[0].ConfidenceScore.Should().BeApproximately(Confidence, 0.001f);
    }

    [Fact]
    public void ChunkPage_LongText_ReturnsMultipleChunksWithNonEmptyContent()
    {
        // ARRANGE
        var sut = CreateSut();
        // ~5600 chars = ~2.8 chunks at 2000 chars
        var longText = string.Concat(Enumerable.Repeat("Lorem ipsum dolor sit amet. ", 200));

        // ACT
        var chunks = sut.ChunkPage(BatchId, PageId, PageNumber, longText, Language, Confidence);

        // ASSERT
        chunks.Should().HaveCountGreaterThan(1);
        chunks.Should().AllSatisfy(c =>
        {
            c.TextContent.Should().NotBeNullOrWhiteSpace();
            c.CharLength.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public void ChunkPage_EmptyText_ReturnsEmptyList()
    {
        // ARRANGE
        var sut = CreateSut();

        // ACT
        var chunks = sut.ChunkPage(BatchId, PageId, PageNumber, "", Language, Confidence);

        // ASSERT
        chunks.Should().BeEmpty();
    }

    [Fact]
    public void ChunkPage_WhitespaceOnlyText_ReturnsEmptyList()
    {
        // ARRANGE
        var sut = CreateSut();

        // ACT
        var chunks = sut.ChunkPage(BatchId, PageId, PageNumber, "   \n\t  ", Language, Confidence);

        // ASSERT
        chunks.Should().BeEmpty();
    }

    [Fact]
    public void ChunkPage_LongText_ChunkIndicesAreSequential()
    {
        // ARRANGE
        var sut = CreateSut();
        var longText = string.Concat(Enumerable.Repeat("Sentence content here. ", 300));

        // ACT
        var chunks = sut.ChunkPage(BatchId, PageId, PageNumber, longText, Language, Confidence);

        // ASSERT
        for (var i = 0; i < chunks.Count; i++)
        {
            chunks[i].ChunkIndex.Should().Be(i,
                because: $"chunk at position {i} should have ChunkIndex={i}");
        }
    }

    [Fact]
    public void ChunkPage_LongText_PreservesMetadataOnAllChunks()
    {
        // ARRANGE
        var sut = CreateSut();
        var longText = string.Concat(Enumerable.Repeat("A paragraph of text. ", 150));

        // ACT
        var chunks = sut.ChunkPage(BatchId, PageId, PageNumber, longText, "it", 0.85f);

        // ASSERT
        chunks.Should().AllSatisfy(c =>
        {
            c.PhotoBatchUploadId.Should().Be(BatchId);
            c.PhotoBatchPageId.Should().Be(PageId);
            c.PageNumber.Should().Be(PageNumber);
            c.Language.Should().Be("it");
            c.ConfidenceScore.Should().BeApproximately(0.85f, 0.001f);
        });
    }

    [Fact]
    public void ChunkPage_ExactlyChunkSizeText_ReturnsSingleChunk()
    {
        // ARRANGE
        var sut = CreateSut();
        // Exactly 2000 chars — should produce exactly one chunk (no split needed)
        var text = new string('x', 2000);

        // ACT
        var chunks = sut.ChunkPage(BatchId, PageId, PageNumber, text, Language, Confidence);

        // ASSERT
        chunks.Should().HaveCount(1);
    }

    [Fact]
    public void ChunkPage_TextSlightlyOverChunkSize_ReturnsTwoChunks()
    {
        // ARRANGE
        var sut = CreateSut();
        // 2001 chars forces a second chunk
        var text = new string('y', 2001);

        // ACT
        var chunks = sut.ChunkPage(BatchId, PageId, PageNumber, text, Language, Confidence);

        // ASSERT — with overlap the second chunk will exist
        chunks.Should().HaveCountGreaterThanOrEqualTo(2);
    }
}

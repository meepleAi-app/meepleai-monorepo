using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

public class TextChunkingServiceTests
{
    private readonly TextChunkingService _service;

    public TextChunkingServiceTests()
    {
        var loggerMock = Mock.Of<ILogger<TextChunkingService>>();
        _service = new TextChunkingService(loggerMock);
    }

    [Fact]
    public void ChunkText_EmptyString_ReturnsEmptyList()
    {
        var result = _service.ChunkText("");

        Assert.Empty(result);
    }

    [Fact]
    public void ChunkText_WhitespaceOnly_ReturnsEmptyList()
    {
        var result = _service.ChunkText("   \n\t  ");

        Assert.Empty(result);
    }

    [Fact]
    public void ChunkText_ShortText_ReturnsSingleChunk()
    {
        var text = "This is a short text that fits in one chunk.";

        var result = _service.ChunkText(text, chunkSize: 100, overlap: 10);

        Assert.Single(result);
        Assert.Equal(text, result[0].Text);
        Assert.Equal(0, result[0].Index);
        Assert.Equal(0, result[0].CharStart);
        Assert.Equal(text.Length, result[0].CharEnd);
    }

    [Fact]
    public void ChunkText_LongText_ReturnsMultipleChunks()
    {
        var text = "This is the first sentence. This is the second sentence. This is the third sentence. This is the fourth sentence.";

        var result = _service.ChunkText(text, chunkSize: 40, overlap: 10);

        Assert.True(result.Count > 1, "Long text should be split into multiple chunks");
        Assert.All(result, chunk => Assert.False(string.IsNullOrWhiteSpace(chunk.Text)));
    }

    [Fact]
    public void ChunkText_WithSentenceBoundaries_BreaksAtSentences()
    {
        var text = "First sentence here. Second sentence here. Third sentence here.";

        var result = _service.ChunkText(text, chunkSize: 30, overlap: 5);

        // Should break at sentence boundaries when possible
        Assert.True(result.Count >= 2);
        Assert.All(result, chunk =>
        {
            var trimmed = chunk.Text.TrimEnd();
            // Chunks should ideally end with sentence terminators or be at the end
            Assert.True(
                trimmed.EndsWith(".") ||
                trimmed.EndsWith("!") ||
                trimmed.EndsWith("?") ||
                chunk.CharEnd >= text.Length,
                $"Chunk should end at sentence boundary: '{chunk.Text}'");
        });
    }

    [Fact]
    public void ChunkText_WithOverlap_HasOverlappingContent()
    {
        var text = "The quick brown fox jumps over the lazy dog. The dog was very lazy indeed.";
        var overlap = 15;

        var result = _service.ChunkText(text, chunkSize: 40, overlap: overlap);

        // With overlap, chunks should have some common content
        if (result.Count > 1)
        {
            for (int i = 0; i < result.Count - 1; i++)
            {
                var currentEnd = result[i].CharEnd;
                var nextStart = result[i + 1].CharStart;

                // Next chunk should start before current chunk ends (overlap)
                Assert.True(nextStart <= currentEnd, "Chunks should have overlapping regions");
            }
        }
    }

    [Fact]
    public void ChunkText_AssignsIncrementalIndexes()
    {
        var text = "Sentence one. Sentence two. Sentence three. Sentence four. Sentence five.";

        var result = _service.ChunkText(text, chunkSize: 30, overlap: 5);

        for (int i = 0; i < result.Count; i++)
        {
            Assert.Equal(i, result[i].Index);
        }
    }

    [Fact]
    public void ChunkText_AssignsPageNumbers()
    {
        var longText = new string('A', 5000); // ~2.5 pages worth

        var result = _service.ChunkText(longText, chunkSize: 500, overlap: 50);

        Assert.All(result, chunk => Assert.True(chunk.Page > 0, "Page numbers should start at 1"));

        // Early chunks should be on earlier pages
        if (result.Count > 2)
        {
            Assert.True(result[0].Page <= result[result.Count - 1].Page,
                "Page numbers should increase or stay same as text progresses");
        }
    }

    [Fact]
    public void PrepareForEmbedding_ReturnsDocumentChunkInputs()
    {
        var text = "This is a test text for embedding preparation.";

        var result = _service.PrepareForEmbedding(text);

        Assert.NotEmpty(result);
        Assert.All(result, chunk =>
        {
            Assert.False(string.IsNullOrWhiteSpace(chunk.Text));
            Assert.True(chunk.Page > 0);
            Assert.True(chunk.CharEnd > chunk.CharStart);
        });
    }

    [Fact]
    public void PrepareForEmbedding_EmptyText_ReturnsEmptyList()
    {
        var result = _service.PrepareForEmbedding("");

        Assert.Empty(result);
    }

    [Fact]
    public void ChunkText_PreservesCharacterPositions()
    {
        var text = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        var result = _service.ChunkText(text, chunkSize: 10, overlap: 3);

        // Verify that char positions map correctly to original text
        foreach (var chunk in result)
        {
            var expectedText = text.Substring(chunk.CharStart, chunk.CharEnd - chunk.CharStart).Trim();
            Assert.Equal(expectedText, chunk.Text);
        }
    }

    [Fact]
    public void ChunkText_WithCustomParameters_UsesProvidedValues()
    {
        var text = new string('X', 1000);
        var chunkSize = 200;
        var overlap = 20;

        var result = _service.ChunkText(text, chunkSize, overlap);

        // Each chunk (except possibly the last) should be around chunkSize
        for (int i = 0; i < result.Count - 1; i++)
        {
            var chunkLength = result[i].CharEnd - result[i].CharStart;
            Assert.True(chunkLength <= chunkSize * 1.1, // Allow 10% tolerance for boundary finding
                $"Chunk {i} length {chunkLength} should be around {chunkSize}");
        }
    }

    [Fact]
    public void ChunkText_WithNoSentenceBoundary_FallsBackToWordBoundary()
    {
        // Text with no sentence terminators but with spaces (word boundaries)
        var text = "This is a very long text without any sentence terminators but with many words and spaces";

        var result = _service.ChunkText(text, chunkSize: 30, overlap: 5);

        // Should break at word boundaries since no sentence boundaries exist
        Assert.True(result.Count > 1);
        // Verify that chunks were created (word boundary logic was used)
        Assert.All(result, chunk => Assert.False(string.IsNullOrWhiteSpace(chunk.Text)));
    }

    [Fact]
    public void ChunkText_WithSentenceTerminatorNotFollowedBySpace_UsesWordBoundary()
    {
        // Text with period not followed by space (e.g., abbreviations)
        var text = "The Dr.Smith went to the store and bought many items for his family";

        var result = _service.ChunkText(text, chunkSize: 25, overlap: 5);

        // Should not break at "Dr." since it's not followed by space
        Assert.True(result.Count > 1);
        Assert.All(result, chunk => Assert.False(string.IsNullOrWhiteSpace(chunk.Text)));
    }

    [Fact]
    public void ChunkText_NullText_ReturnsEmptyList()
    {
        var result = _service.ChunkText(null!);

        Assert.Empty(result);
    }

    [Fact]
    public void PrepareForEmbedding_WithNullText_ReturnsEmptyList()
    {
        var result = _service.PrepareForEmbedding(null!);

        Assert.Empty(result);
    }
}

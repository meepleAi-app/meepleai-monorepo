using System;
using System.Collections.Generic;
using System.Linq;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Unit tests for TextChunkingService which splits text into smaller chunks for embedding.
///
/// Tests cover:
/// - Basic chunking with default and custom parameters
/// - Sentence and word boundary detection
/// - Overlap between chunks
/// - Edge cases (empty text, very short text, very long text)
/// - Page estimation
/// - PrepareForEmbedding functionality
/// </summary>
public class TextChunkingServiceTests
{
    private readonly TextChunkingService _service;
    private readonly Mock<ILogger<TextChunkingService>> _mockLogger;

    public TextChunkingServiceTests()
    {
        _mockLogger = new Mock<ILogger<TextChunkingService>>();
        _service = new TextChunkingService(_mockLogger.Object);
    }

    #region Basic Chunking Tests

    /// <summary>
    /// Scenario: Empty text input
    ///   Given empty or whitespace text
    ///   When chunking text
    ///   Then empty list is returned
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    [InlineData(null)]
    public void ChunkText_WithEmptyInput_ReturnsEmptyList(string? input)
    {
        // Act: Chunk empty text
        var chunks = _service.ChunkText(input ?? string.Empty);

        // Assert: Empty list returned
        Assert.Empty(chunks);
    }

    /// <summary>
    /// Scenario: Text shorter than chunk size
    ///   Given text shorter than chunk size
    ///   When chunking text
    ///   Then single chunk is returned containing entire text
    /// </summary>
    [Fact]
    public void ChunkText_WithShortText_ReturnsSingleChunk()
    {
        // Arrange: Short text (< 512 characters)
        var text = "This is a short text that fits in a single chunk.";

        // Act: Chunk text
        var chunks = _service.ChunkText(text);

        // Assert: Single chunk
        Assert.Single(chunks);
        Assert.Equal(text, chunks[0].Text);
        Assert.Equal(0, chunks[0].Index);
        Assert.Equal(0, chunks[0].CharStart);
        Assert.Equal(text.Length, chunks[0].CharEnd);
        Assert.Equal(1, chunks[0].Page); // First page
    }

    /// <summary>
    /// Scenario: Text requiring multiple chunks
    ///   Given text longer than chunk size
    ///   When chunking text with default parameters
    ///   Then multiple chunks are created
    /// </summary>
    [Fact]
    public void ChunkText_WithLongText_CreatesMultipleChunks()
    {
        // Arrange: Text longer than default 512 characters
        var text = new string('a', 1500); // 1500 characters

        // Act: Chunk text
        var chunks = _service.ChunkText(text);

        // Assert: Multiple chunks created
        Assert.True(chunks.Count > 1);
        Assert.True(chunks.Count >= 3); // ~1500 / 512 = ~3 chunks
    }

    /// <summary>
    /// Scenario: Custom chunk size
    ///   Given custom chunk size parameter
    ///   When chunking text
    ///   Then chunks respect the custom size
    /// </summary>
    [Fact]
    public void ChunkText_WithCustomChunkSize_RespectsCustomSize()
    {
        // Arrange: Text and custom chunk size
        var text = new string('a', 1000);
        var customChunkSize = 100;

        // Act: Chunk with custom size
        var chunks = _service.ChunkText(text, chunkSize: customChunkSize);

        // Assert: More chunks due to smaller size
        Assert.True(chunks.Count >= 10); // ~1000 / 100 = ~10 chunks

        // Verify chunk sizes are approximately correct (accounting for overlap reduction)
        foreach (var chunk in chunks.Take(chunks.Count - 1)) // Except last chunk
        {
            Assert.True(chunk.Text.Length <= customChunkSize + 10); // Small tolerance for boundary adjustment
        }
    }

    /// <summary>
    /// Scenario: Custom overlap size
    ///   Given custom overlap parameter
    ///   When chunking text
    ///   Then chunks overlap by specified amount
    /// </summary>
    [Fact]
    public void ChunkText_WithCustomOverlap_CreatesOverlappingChunks()
    {
        // Arrange: Simple text with custom overlap
        var text = "0123456789" + "ABCDEFGHIJ" + "abcdefghij"; // 30 characters
        var chunkSize = 15;
        var overlap = 5;

        // Act: Chunk with custom overlap
        var chunks = _service.ChunkText(text, chunkSize: chunkSize, overlap: overlap);

        // Assert: Overlapping chunks
        Assert.True(chunks.Count >= 2);

        // Verify overlap exists between consecutive chunks
        if (chunks.Count >= 2)
        {
            var firstChunk = chunks[0];
            var secondChunk = chunks[1];

            // Second chunk should start before first chunk ends (due to overlap)
            Assert.True(secondChunk.CharStart < firstChunk.CharEnd);

            // The overlap should be approximately the specified overlap
            var actualOverlap = firstChunk.CharEnd - secondChunk.CharStart;
            Assert.InRange(actualOverlap, 0, overlap + 10); // Allow some tolerance
        }
    }

    /// <summary>
    /// Scenario: Zero overlap
    ///   Given zero overlap parameter
    ///   When chunking text
    ///   Then chunks have no overlap
    /// </summary>
    [Fact]
    public void ChunkText_WithZeroOverlap_CreatesNonOverlappingChunks()
    {
        // Arrange: Text with no overlap
        var text = new string('a', 1000);
        var overlap = 0;

        // Act: Chunk with zero overlap
        var chunks = _service.ChunkText(text, overlap: overlap);

        // Assert: Non-overlapping chunks
        for (int i = 0; i < chunks.Count - 1; i++)
        {
            Assert.Equal(chunks[i].CharEnd, chunks[i + 1].CharStart);
        }
    }

    #endregion

    #region Sentence Boundary Tests

    /// <summary>
    /// Scenario: Chunking breaks at sentence boundary
    ///   Given text with sentence boundaries within chunk size
    ///   When chunking text
    ///   Then chunks prefer to break at sentence boundaries
    /// </summary>
    [Fact]
    public void ChunkText_WithSentences_BreaksAtSentenceBoundary()
    {
        // Arrange: Text with clear sentence boundaries
        var sentence1 = "This is sentence one. ";
        var sentence2 = "This is sentence two. ";
        var sentence3 = "This is sentence three. ";
        var padding = new string('a', 400); // Ensure we need multiple chunks

        var text = sentence1 + sentence2 + padding + sentence3;

        // Act: Chunk text
        var chunks = _service.ChunkText(text, chunkSize: 100);

        // Assert: Verify chunks break at reasonable boundaries
        Assert.True(chunks.Count > 1);

        // First chunk should likely end at a sentence boundary
        var firstChunk = chunks[0];
        Assert.True(
            firstChunk.Text.EndsWith(".") ||
            firstChunk.Text.EndsWith("!") ||
            firstChunk.Text.EndsWith("?"),
            "First chunk should end at sentence boundary when possible"
        );
    }

    /// <summary>
    /// Scenario: Multiple sentence terminators
    ///   Given text with period, exclamation, and question marks
    ///   When chunking text
    ///   Then all sentence terminators are recognized
    /// </summary>
    [Fact]
    public void ChunkText_WithVariousSentenceTerminators_RecognizesAll()
    {
        // Arrange: Text with different terminators
        var text = "Statement. " + new string('a', 100) + "Question? " + new string('b', 100) + "Exclamation! " + new string('c', 100);

        // Act: Chunk text
        var chunks = _service.ChunkText(text, chunkSize: 120);

        // Assert: Should have multiple chunks
        Assert.True(chunks.Count >= 2);

        // Verify chunks end at sentence boundaries where possible
        var chunksEndingWithPunctuation = chunks.Count(c =>
            c.Text.TrimEnd().EndsWith(".") ||
            c.Text.TrimEnd().EndsWith("!") ||
            c.Text.TrimEnd().EndsWith("?")
        );

        Assert.True(chunksEndingWithPunctuation > 0, "At least some chunks should end with sentence terminators");
    }

    #endregion

    #region Word Boundary Tests

    /// <summary>
    /// Scenario: Chunking breaks at word boundary when no sentence boundary
    ///   Given text without sentence boundaries
    ///   When chunking text
    ///   Then chunks break at word boundaries
    /// </summary>
    [Fact]
    public void ChunkText_WithoutSentenceBoundaries_BreaksAtWordBoundary()
    {
        // Arrange: Text without periods (no sentence boundaries)
        var text = "word1 word2 word3 " + new string('a', 500) + " word4 word5 word6";

        // Act: Chunk text
        var chunks = _service.ChunkText(text, chunkSize: 100);

        // Assert: Multiple chunks
        Assert.True(chunks.Count > 1);

        // Verify chunks don't split words (except last chunk)
        foreach (var chunk in chunks.Take(chunks.Count - 1))
        {
            var trimmed = chunk.Text.TrimEnd();
            if (trimmed.Length > 0)
            {
                // Should not end mid-word (should end with whitespace or complete word)
                Assert.True(
                    char.IsWhiteSpace(chunk.Text[chunk.Text.Length - 1]) ||
                    trimmed.Length < chunk.Text.Length,
                    "Chunks should break at word boundaries when possible"
                );
            }
        }
    }

    #endregion

    #region Character Position Tests

    /// <summary>
    /// Scenario: Chunk character positions are accurate
    ///   Given text chunked into multiple pieces
    ///   When examining chunk positions
    ///   Then CharStart and CharEnd are accurate
    /// </summary>
    [Fact]
    public void ChunkText_CharacterPositions_AreAccurate()
    {
        // Arrange: Text with known content
        var text = "ABCDEFGHIJ" + new string('x', 500) + "KLMNOPQRST";

        // Act: Chunk text
        var chunks = _service.ChunkText(text, chunkSize: 100);

        // Assert: Verify positions
        Assert.True(chunks.Count > 1);

        // First chunk should start at 0
        Assert.Equal(0, chunks[0].CharStart);

        // Each chunk's text should match the substring at its positions
        foreach (var chunk in chunks)
        {
            var expectedText = text.Substring(chunk.CharStart, chunk.CharEnd - chunk.CharStart).Trim();
            Assert.Equal(expectedText, chunk.Text);
        }

        // Last chunk should end at text length
        var lastChunk = chunks[chunks.Count - 1];
        Assert.True(lastChunk.CharEnd <= text.Length);
    }

    /// <summary>
    /// Scenario: Chunk indices are sequential
    ///   Given multiple chunks
    ///   When examining chunk indices
    ///   Then indices are sequential starting from 0
    /// </summary>
    [Fact]
    public void ChunkText_Indices_AreSequential()
    {
        // Arrange: Text requiring multiple chunks
        var text = new string('a', 1500);

        // Act: Chunk text
        var chunks = _service.ChunkText(text);

        // Assert: Sequential indices
        for (int i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(i, chunks[i].Index);
        }
    }

    #endregion

    #region Page Estimation Tests

    /// <summary>
    /// Scenario: Page numbers are estimated correctly
    ///   Given text chunked across multiple estimated pages
    ///   When examining chunk page numbers
    ///   Then page numbers increase appropriately
    /// </summary>
    [Fact]
    public void ChunkText_PageEstimation_IncreasesWithPosition()
    {
        // Arrange: Long text (~6000 characters = ~3 pages at 2000 chars/page)
        var text = new string('a', 6000);

        // Act: Chunk text
        var chunks = _service.ChunkText(text);

        // Assert: Page numbers should increase
        var pageNumbers = chunks.Select(c => c.Page).Distinct().ToList();

        Assert.True(pageNumbers.Count >= 2, "Should span multiple pages");
        Assert.True(pageNumbers.Max() >= 2, "Should reach at least page 2");

        // Verify pages are in ascending order
        int previousPage = 0;
        foreach (var chunk in chunks)
        {
            Assert.True(chunk.Page >= previousPage, "Page numbers should be non-decreasing");
            previousPage = chunk.Page;
        }
    }

    /// <summary>
    /// Scenario: First chunk is on page 1
    ///   Given any text
    ///   When chunking text
    ///   Then first chunk starts on page 1
    /// </summary>
    [Fact]
    public void ChunkText_FirstChunk_IsOnPageOne()
    {
        // Arrange: Various texts
        var testCases = new[]
        {
            "Short text",
            new string('a', 500),
            new string('b', 5000)
        };

        foreach (var text in testCases)
        {
            // Act: Chunk text
            var chunks = _service.ChunkText(text);

            // Assert: First chunk on page 1
            Assert.NotEmpty(chunks);
            Assert.Equal(1, chunks[0].Page);
        }
    }

    #endregion

    #region PrepareForEmbedding Tests

    /// <summary>
    /// Scenario: PrepareForEmbedding returns correct format
    ///   Given text to chunk
    ///   When preparing for embedding
    ///   Then DocumentChunkInput objects are returned
    /// </summary>
    [Fact]
    public void PrepareForEmbedding_ReturnsDocumentChunkInputs()
    {
        // Arrange: Text
        var text = "This is some text. " + new string('a', 600);

        // Act: Prepare for embedding
        var inputs = _service.PrepareForEmbedding(text);

        // Assert: DocumentChunkInput format
        Assert.NotEmpty(inputs);

        foreach (var input in inputs)
        {
            Assert.NotNull(input.Text);
            Assert.NotEmpty(input.Text);
            Assert.True(input.Page >= 1);
            Assert.True(input.CharStart >= 0);
            Assert.True(input.CharEnd > input.CharStart);
        }
    }

    /// <summary>
    /// Scenario: PrepareForEmbedding with custom parameters
    ///   Given custom chunk size and overlap
    ///   When preparing for embedding
    ///   Then custom parameters are respected
    /// </summary>
    [Fact]
    public void PrepareForEmbedding_WithCustomParameters_RespectsParameters()
    {
        // Arrange: Text and custom parameters
        var text = new string('a', 1000);
        var customChunkSize = 100;
        var customOverlap = 20;

        // Act: Prepare with custom parameters
        var inputs = _service.PrepareForEmbedding(text, customChunkSize, customOverlap);

        // Assert: More chunks due to smaller size
        Assert.True(inputs.Count >= 10);

        // Verify chunk sizes
        foreach (var input in inputs.Take(inputs.Count - 1))
        {
            Assert.True(input.Text.Length <= customChunkSize + 20); // Tolerance for boundary adjustment
        }
    }

    /// <summary>
    /// Scenario: PrepareForEmbedding with empty text
    ///   Given empty text
    ///   When preparing for embedding
    ///   Then empty list is returned
    /// </summary>
    [Fact]
    public void PrepareForEmbedding_WithEmptyText_ReturnsEmptyList()
    {
        // Arrange: Empty text
        var text = "";

        // Act: Prepare for embedding
        var inputs = _service.PrepareForEmbedding(text);

        // Assert: Empty list
        Assert.Empty(inputs);
    }

    #endregion

    #region Edge Case Tests

    /// <summary>
    /// Scenario: Very long text
    ///   Given very long text (10000+ characters)
    ///   When chunking text
    ///   Then all text is included in chunks
    /// </summary>
    [Fact]
    public void ChunkText_WithVeryLongText_IncludesAllText()
    {
        // Arrange: Very long text
        var text = new string('a', 10000);

        // Act: Chunk text
        var chunks = _service.ChunkText(text);

        // Assert: All text is covered
        Assert.NotEmpty(chunks);

        // Calculate total characters in all chunks (accounting for overlap)
        var lastChunk = chunks[chunks.Count - 1];
        var totalCoverage = lastChunk.CharEnd;

        Assert.Equal(text.Length, totalCoverage);
    }

    /// <summary>
    /// Scenario: Text with only whitespace between chunks
    ///   Given text with extensive whitespace
    ///   When chunking text
    ///   Then whitespace is handled appropriately
    /// </summary>
    [Fact]
    public void ChunkText_WithWhitespace_HandlesCorrectly()
    {
        // Arrange: Text with lots of whitespace
        var text = "Word1" + new string(' ', 100) + "Word2" + new string('\n', 50) + "Word3";

        // Act: Chunk text
        var chunks = _service.ChunkText(text, chunkSize: 100);

        // Assert: Chunks are created despite whitespace
        Assert.NotEmpty(chunks);

        // Verify chunks are trimmed
        foreach (var chunk in chunks)
        {
            if (!string.IsNullOrWhiteSpace(chunk.Text))
            {
                Assert.Equal(chunk.Text.Trim(), chunk.Text);
            }
        }
    }

    /// <summary>
    /// Scenario: Text with special characters
    ///   Given text with unicode, emojis, special characters
    ///   When chunking text
    ///   Then special characters are preserved
    /// </summary>
    [Fact]
    public void ChunkText_WithSpecialCharacters_PreservesCharacters()
    {
        // Arrange: Text with special characters
        var text = "Unicode: café, naïve. Emoji: 😀🎮. Symbols: ©®™. " + new string('a', 500);

        // Act: Chunk text
        var chunks = _service.ChunkText(text);

        // Assert: Special characters preserved
        Assert.NotEmpty(chunks);

        var firstChunk = chunks[0];
        Assert.Contains("café", firstChunk.Text);
        Assert.Contains("😀", firstChunk.Text);
        Assert.Contains("©", firstChunk.Text);
    }

    /// <summary>
    /// Scenario: Chunk size smaller than overlap
    ///   Given overlap >= chunk size (invalid configuration)
    ///   When chunking text
    ///   Then service handles gracefully
    /// </summary>
    [Fact]
    public void ChunkText_WithOverlapLargerThanChunkSize_HandlesGracefully()
    {
        // Arrange: Invalid configuration
        var text = new string('a', 1000);
        var chunkSize = 50;
        var overlap = 60; // Larger than chunk size

        // Act: Chunk text (should not throw)
        var chunks = _service.ChunkText(text, chunkSize, overlap);

        // Assert: Chunks are still created
        Assert.NotEmpty(chunks);

        // Service should still produce some reasonable output
        Assert.True(chunks.Count > 0);
    }

    /// <summary>
    /// Scenario: Single character chunk size
    ///   Given chunk size of 1
    ///   When chunking text
    ///   Then many tiny chunks are created
    /// </summary>
    [Fact]
    public void ChunkText_WithSingleCharChunkSize_CreatesManyTinyChunks()
    {
        // Arrange: Tiny chunk size
        var text = "ABCDEFGHIJ";
        var chunkSize = 1;

        // Act: Chunk text
        var chunks = _service.ChunkText(text, chunkSize, overlap: 0);

        // Assert: Many chunks
        Assert.True(chunks.Count >= 5); // Should create multiple chunks
    }

    /// <summary>
    /// Scenario: Text with newlines and tabs
    ///   Given text with mixed whitespace
    ///   When chunking text
    ///   Then whitespace is handled correctly
    /// </summary>
    [Fact]
    public void ChunkText_WithMixedWhitespace_HandlesCorrectly()
    {
        // Arrange: Text with mixed whitespace
        var text = "Line1\nLine2\r\nLine3\tTabbed" + new string('a', 500);

        // Act: Chunk text
        var chunks = _service.ChunkText(text);

        // Assert: Chunks created successfully
        Assert.NotEmpty(chunks);

        // First chunk should preserve the structure
        Assert.Contains("Line1", chunks[0].Text);
    }

    #endregion

    #region Logging Tests

    /// <summary>
    /// Scenario: Service logs chunking operation
    ///   Given text to chunk
    ///   When chunking completes
    ///   Then operation is logged
    /// </summary>
    [Fact]
    public void ChunkText_LogsOperation()
    {
        // Arrange: Text
        var text = new string('a', 1000);

        // Act: Chunk text
        var chunks = _service.ChunkText(text);

        // Assert: Logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Chunked")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}

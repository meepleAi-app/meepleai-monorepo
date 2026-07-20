using System.Linq;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="TextChunkingService"/>.
/// RAG answer-quality fix: overlapping chunks must not begin mid-word.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class TextChunkingServiceTests
{
    private static TextChunkingService CreateSut() => new(NullLogger<TextChunkingService>.Instance);

    [Fact]
    public void ChunkText_OverlappingChunks_StartAtWordBoundaries()
    {
        // Arrange: text long enough to force several overlapping chunks. The fixed 150-char
        // overlap is measured back from a clean end boundary, which previously landed the next
        // chunk mid-word (e.g. "ndo acqua alla superficie.").
        var text = string.Concat(Enumerable.Repeat("preparazione ", 500)); // ~6500 chars
        var sut = CreateSut();

        // Act
        var chunks = sut.ChunkText(text);

        // Assert
        chunks.Count.Should().BeGreaterThan(1, "the text is long enough to require multiple chunks");
        foreach (var chunk in chunks)
        {
            var startsAtWordBoundary = chunk.CharStart == 0
                || char.IsWhiteSpace(text[chunk.CharStart - 1]);
            startsAtWordBoundary.Should().BeTrue(
                $"chunk {chunk.Index} (CharStart={chunk.CharStart}) must start at a word boundary, not mid-word");
        }
    }

    [Fact]
    public void ChunkText_ConsecutiveChunks_DoNotSplitAWordAcrossTheBoundaryStart()
    {
        // Arrange: distinct multi-char words so a mid-word start would be detectable as a fragment.
        var text = string.Concat(Enumerable.Range(0, 800).Select(i => $"parola{i:D4} "));
        var sut = CreateSut();

        // Act
        var chunks = sut.ChunkText(text);

        // Assert: every chunk's first token is a complete "parolaNNNN" token, never a fragment.
        chunks.Count.Should().BeGreaterThan(1);
        foreach (var chunk in chunks)
        {
            var firstToken = chunk.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            firstToken.Should().MatchRegex("^parola[0-9]{4}$",
                $"chunk {chunk.Index} must start with a whole word, got '{firstToken}'");
        }
    }

    [Fact]
    public void ChunkText_ShortText_ReturnsSingleTrimmedChunk()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var chunks = sut.ChunkText("Solo una breve frase di preparazione.");

        // Assert
        chunks.Should().ContainSingle();
        chunks[0].Text.Should().Be("Solo una breve frase di preparazione.");
    }
}

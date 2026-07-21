using Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;
using Api.Constants;
using Api.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public class HeadingAwareChunkAdapterTests
{
    [Fact]
    public void ToChunkInputs_PreservesIdentityAndHierarchyFields()
    {
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var chunks = new List<DocumentChunk>
        {
            new() { Id = parentId, Text = "Setup", Page = 2, CharStart = 10, CharEnd = 15,
                    Heading = "Setup", Level = 0, ParentChunkId = null, ElementType = "Title" },
            new() { Id = childId, Text = "Place 3 tiles", Page = 2, CharStart = 16, CharEnd = 29,
                    Heading = "Setup", Level = 2, ParentChunkId = parentId, ElementType = "NarrativeText" },
        };

        var result = HeadingAwareChunkAdapter.ToChunkInputs(chunks);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(parentId);
        result[0].Heading.Should().Be("Setup");
        result[0].Level.Should().Be((short)0);
        result[0].ElementType.Should().Be("Title");
        result[1].Id.Should().Be(childId);
        result[1].ParentChunkId.Should().Be(parentId);
        result[1].Level.Should().Be((short)2);
        result[1].Text.Should().Be("Place 3 tiles");
        result[1].CharStart.Should().Be(16);
    }

    [Fact]
    public void CapForEmbedding_TruncatesOnlyWhenOverLimit()
    {
        var under = new string('a', ChunkingConstants.MaxEmbeddingChars);
        var over = new string('b', ChunkingConstants.MaxEmbeddingChars + 500);

        HeadingAwareChunkAdapter.CapForEmbedding(under).Should().HaveLength(ChunkingConstants.MaxEmbeddingChars);
        HeadingAwareChunkAdapter.CapForEmbedding(over).Should().HaveLength(ChunkingConstants.MaxEmbeddingChars);
        HeadingAwareChunkAdapter.CapForEmbedding("short").Should().Be("short");
    }
}

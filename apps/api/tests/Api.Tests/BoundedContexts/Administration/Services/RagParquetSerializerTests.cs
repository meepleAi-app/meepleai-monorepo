using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Services;

/// <summary>
/// Unit tests for RagParquetSerializer — validates round-trip fidelity for chunks and embeddings.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class RagParquetSerializerTests
{
    [Fact]
    public async Task ChunkRoundTrip_PreservesData()
    {
        // Arrange
        var chunks = new List<RagExportChunkLine>
        {
            new(ChunkIndex: 0, PageNumber: 1,    Content: "First chunk content",  CharacterCount: 19),
            new(ChunkIndex: 1, PageNumber: null,  Content: "Second chunk content", CharacterCount: 20)
        };

        // Act
        var bytes = await RagParquetSerializer.SerializeChunksAsync(chunks);
        var deserialized = await RagParquetSerializer.DeserializeChunksAsync(bytes);

        // Assert
        deserialized.Should().HaveCount(2);

        deserialized[0].ChunkIndex.Should().Be(0);
        deserialized[0].PageNumber.Should().Be(1);
        deserialized[0].Content.Should().Be("First chunk content");
        deserialized[0].CharacterCount.Should().Be(19);

        deserialized[1].ChunkIndex.Should().Be(1);
        deserialized[1].PageNumber.Should().BeNull();
        deserialized[1].Content.Should().Be("Second chunk content");
        deserialized[1].CharacterCount.Should().Be(20);
    }

    [Fact]
    public async Task EmbeddingRoundTrip_PreservesVectors()
    {
        // Arrange
        var vector = new float[] { 0.12345f, -0.98765f, 0.55555f };
        var embeddings = new List<RagExportEmbeddingLine>
        {
            new(ChunkIndex: 0, PageNumber: 2, TextContent: "Embedding text", Vector: vector, Model: "text-embedding-3-small")
        };

        // Act
        var bytes = await RagParquetSerializer.SerializeEmbeddingsAsync(embeddings);
        var deserialized = await RagParquetSerializer.DeserializeEmbeddingsAsync(bytes);

        // Assert
        deserialized.Should().HaveCount(1);

        var result = deserialized[0];
        result.ChunkIndex.Should().Be(0);
        result.PageNumber.Should().Be(2);
        result.TextContent.Should().Be("Embedding text");
        result.Model.Should().Be("text-embedding-3-small");

        result.Vector.Should().HaveCount(vector.Length);
        for (var i = 0; i < vector.Length; i++)
            result.Vector[i].Should().BeApproximately(vector[i], precision: 0.00001f);
    }
}

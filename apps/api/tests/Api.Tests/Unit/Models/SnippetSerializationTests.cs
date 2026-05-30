using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Models;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Models;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class SnippetSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    [Fact]
    public void Snippet_WithoutChunkFields_SerializesWithoutChunkKeys()
    {
        // Arrange: legacy construction (no chunkId / chunkPosition)
        var snippet = new Snippet("text", "source", 1, 0, 0.9f);

        // Act
        var json = JsonSerializer.Serialize(snippet, JsonOptions);

        // Assert: ZERO drift in wire format for legacy call sites
        json.Should().NotContain("chunkId");
        json.Should().NotContain("chunkPosition");
        json.Should().Contain("\"text\":\"text\"");
        json.Should().Contain("\"source\":\"source\"");
    }

    [Fact]
    public void Snippet_WithChunkFields_SerializesWithBothKeys()
    {
        // Arrange: cross-game call site
        var snippet = new Snippet("text", "src", 14, 0, 0.85f)
        {
            chunkId = "abc_3",
            chunkPosition = 3,
        };

        // Act
        var json = JsonSerializer.Serialize(snippet, JsonOptions);

        // Assert
        json.Should().Contain("\"chunkId\":\"abc_3\"");
        json.Should().Contain("\"chunkPosition\":3");
    }

    [Fact]
    public void Snippet_WithOnlyChunkId_SerializesOnlyThatKey()
    {
        // Arrange: defensive — partial set
        var snippet = new Snippet("text", "src", 1, 0, 0.5f)
        {
            chunkId = "abc_0",
            // chunkPosition intentionally not set
        };

        // Act
        var json = JsonSerializer.Serialize(snippet, JsonOptions);

        // Assert
        json.Should().Contain("\"chunkId\":\"abc_0\"");
        json.Should().NotContain("chunkPosition");
    }
}

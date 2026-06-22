using System.Text.Json;
using Api.Infrastructure.Serialization;
using Api.Models;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Models;

/// <summary>
/// BC verification: asserts that per-game Snippet serialization via the PRODUCTION SSE
/// JsonSerializerOptions does NOT emit chunkId / chunkPosition keys when those fields
/// are null — i.e., the additive-only contract (D-4) holds.
///
/// The 8 non-cross-game call sites (StreamQa, PlaygroundChat, StreamDebugQa,
/// StreamExplain, RagService ×4) all construct Snippet with the positional ctor
/// only, leaving chunkId and chunkPosition null. This test guards against future
/// drift if SseJsonOptions or System.Text.Json defaults change.
///
/// See: GitHub issue #1702 — BE Snippet chunk-id — D-4 wire-format guard.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class PerGameSnippetBackCompatTests
{
    [Fact]
    public void Snippet_FromPerGameCallSite_SerializesWithoutChunkIdAndChunkPositionKeys()
    {
        // Arrange: legacy Snippet construction — mimics ALL 8 non-cross-game call sites
        // (StreamQa, PlaygroundChat, StreamDebugQa, StreamExplain, RagService ×4)
        var snippet = new Snippet(
            text: "rules text",
            source: "PDF:abc-123",
            page: 14,
            line: 0,
            score: 0.85f);

        var citations = new StreamingCitations(new[] { snippet });

        // Act: serialize with the PRODUCTION SSE options — the same ones used by all SSE endpoints
        var json = JsonSerializer.Serialize(citations, SseJsonOptions.Default);

        // Assert: the new fields MUST NOT appear in the wire format for per-game endpoints
        json.Should().NotContain("chunkId",
            "per-game endpoints MUST NOT emit chunkId (additive-only contract — D-4)");
        json.Should().NotContain("chunkPosition",
            "per-game endpoints MUST NOT emit chunkPosition (additive-only contract — D-4)");

        // Sanity: existing keys MUST still be present (camelCase per SseJsonOptions)
        json.Should().Contain("\"text\":\"rules text\"");
        json.Should().Contain("\"source\":\"PDF:abc-123\"");
        json.Should().Contain("\"page\":14");
        json.Should().Contain("\"line\":0");
        // Note: float serialization may emit "0.85" or "0.85000001" depending on precision;
        // we assert the key is present rather than the exact float string
        json.Should().Contain("\"score\":");
    }

    [Fact]
    public void Snippet_WithChunkFields_ViaProductionOptions_SerializesBothNewKeys()
    {
        // Arrange: cross-game Snippet (populated by CrossGameStreamQaQueryHandler only)
        var snippet = new Snippet("rules text", "PDF:abc-123", 14, 0, 0.85f)
        {
            chunkId = "abc123_3",
            chunkPosition = 3,
        };

        var citations = new StreamingCitations(new[] { snippet });

        // Act
        var json = JsonSerializer.Serialize(citations, SseJsonOptions.Default);

        // Assert: chunkId and chunkPosition MUST appear when set (cross-game /ask/global contract)
        json.Should().Contain("\"chunkId\":\"abc123_3\"");
        json.Should().Contain("\"chunkPosition\":3");
    }
}

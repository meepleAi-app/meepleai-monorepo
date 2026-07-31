using System.Text.Json;
using Api.Infrastructure.Serialization;
using Api.Models;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Models;

/// <summary>
/// SP-C (#3407): the per-game StreamQa Snippet gains additive, Full-gated region fields
/// (regions/charStart/charEnd). Like chunkId/chunkPosition (#1702), they are
/// <c>[JsonIgnore(WhenWritingNull)]</c> so the D-4 additive-only wire contract holds:
/// legacy/Protected snippets (null fields) MUST NOT emit the new keys.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class SnippetRegionSerializationTests
{
    [Fact]
    public void Snippet_WithoutRegions_OmitsRegionKeys_ViaSseOptions()
    {
        var snippet = new Snippet("rules text", "PDF:abc-123", 14, 0, 0.85f);
        var citations = new StreamingCitations(new[] { snippet });

        var json = JsonSerializer.Serialize(citations, SseJsonOptions.Default);

        json.Should().NotContain("regions");
        json.Should().NotContain("charStart");
        json.Should().NotContain("charEnd");
    }

    [Fact]
    public void Snippet_WithRegions_EmitsCamelCaseRegionArray_ViaSseOptions()
    {
        var snippet = new Snippet("rules text", "PDF:abc-123", 14, 0, 0.85f)
        {
            regions = new[] { new CitationRegion(14, 0.1, 0.2, 0.3, 0.4) },
            charStart = 100,
            charEnd = 250,
        };
        var citations = new StreamingCitations(new[] { snippet });

        var json = JsonSerializer.Serialize(citations, SseJsonOptions.Default);

        json.Should().Contain("\"regions\":[");
        json.Should().Contain("\"page\":14");
        json.Should().Contain("\"x\":0.1");
        json.Should().Contain("\"width\":0.3");
        json.Should().Contain("\"charStart\":100");
        json.Should().Contain("\"charEnd\":250");
    }
}

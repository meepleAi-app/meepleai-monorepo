using System.Text.Json;
using Api.Infrastructure.Serialization;
using Api.Models;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Models;

/// <summary>
/// SP-C (#3407): CitationRegion is the FE-facing, Full-gated region shape parsed from the
/// persisted <c>text_chunks.bounding_boxes_json</c> (produced by
/// <c>ChunkBoundingBoxJson.Serialize</c>). The stored JSON is an ARRAY of objects with
/// LOWERCASE keys <c>[{page,x,y,width,height}]</c> and x/y/width/height normalized to
/// [0,1] top-left. Parse must be case-insensitive and defensive (never throw).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class CitationRegionTests
{
    [Fact]
    public void Parse_LowercaseJsonArray_ReturnsRegionWithNormalizedValues()
    {
        // Arrange: EXACT shape persisted by ChunkBoundingBoxJson.Serialize (lowercase keys, array)
        const string json = "[{\"page\":2,\"x\":0.1,\"y\":0.2,\"width\":0.3,\"height\":0.4}]";

        // Act
        var regions = CitationRegion.Parse(json);

        // Assert
        regions.Should().NotBeNull();
        regions!.Should().HaveCount(1);
        var r = regions[0];
        r.Page.Should().Be(2);
        r.X.Should().BeApproximately(0.1, 1e-9);
        r.Y.Should().BeApproximately(0.2, 1e-9);
        r.Width.Should().BeApproximately(0.3, 1e-9);
        r.Height.Should().BeApproximately(0.4, 1e-9);
    }

    [Fact]
    public void Parse_MultipleBoxes_ReturnsAllRegionsInOrder()
    {
        const string json =
            "[{\"page\":1,\"x\":0,\"y\":0,\"width\":0.5,\"height\":0.5}," +
            "{\"page\":1,\"x\":0.5,\"y\":0.5,\"width\":0.4,\"height\":0.4}]";

        var regions = CitationRegion.Parse(json);

        regions.Should().NotBeNull();
        regions!.Should().HaveCount(2);
        regions[0].X.Should().BeApproximately(0, 1e-9);
        regions[1].X.Should().BeApproximately(0.5, 1e-9);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[]")]              // valid JSON but no boxes → treated as "no regions"
    [InlineData("not json")]        // malformed → defensive null, no throw
    [InlineData("{\"page\":1}")]    // object, not the expected array → null
    public void Parse_NullEmptyOrInvalid_ReturnsNull(string? json)
    {
        CitationRegion.Parse(json).Should().BeNull();
    }

    [Fact]
    public void CitationRegion_SerializesCamelCase_ViaSseOptions()
    {
        // The wire contract the FE (zod CitationSchema / mapSseCitation) expects.
        var region = new CitationRegion(3, 0.11, 0.22, 0.33, 0.44);

        var wire = JsonSerializer.Serialize(region, SseJsonOptions.Default);

        wire.Should().Contain("\"page\":3");
        wire.Should().Contain("\"x\":0.11");
        wire.Should().Contain("\"y\":0.22");
        wire.Should().Contain("\"width\":0.33");
        wire.Should().Contain("\"height\":0.44");
    }
}

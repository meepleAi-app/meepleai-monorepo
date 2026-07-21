using Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

[Trait("Category", TestCategories.Unit)]
public class StructuredElementsPayloadTests
{
    [Fact]
    public void RoundTrip_PreservesElements()
    {
        var elements = new List<ExtractedElement> { new("Setup", 1, "Title"), new("body", 2, "NarrativeText") };
        var json = StructuredElementsPayload.Serialize(elements);
        json.Should().NotBeNullOrEmpty();

        var back = StructuredElementsPayload.TryDeserialize(json);
        back.Should().NotBeNull();
        back!.Select(e => (e.Text, e.PageNumber, e.ElementType))
            .Should().Equal(("Setup", 1, "Title"), ("body", 2, "NarrativeText"));
    }

    [Fact]
    public void Serialize_NullOrEmpty_ReturnsNull()
    {
        StructuredElementsPayload.Serialize(null).Should().BeNull();
        StructuredElementsPayload.Serialize(new List<ExtractedElement>()).Should().BeNull();
    }

    [Fact]
    public void TryDeserialize_MalformedOrLegacy_ReturnsNull_NeverThrows()
    {
        StructuredElementsPayload.TryDeserialize("{ not valid json").Should().BeNull();
        StructuredElementsPayload.TryDeserialize(null).Should().BeNull();
        // legacy/unknown shape tolerated (unknown members ignored, missing -> null)
        StructuredElementsPayload.TryDeserialize("{\"SchemaVersion\":99,\"Elements\":null}").Should().BeNull();
    }

    [Fact]
    public void TryDeserialize_FrozenBlob_Reads()
    {
        const string frozen = "{\"SchemaVersion\":1,\"Elements\":[{\"Text\":\"Setup\",\"PageNumber\":1,\"ElementType\":\"Title\"}]}";
        var back = StructuredElementsPayload.TryDeserialize(frozen);
        back.Should().ContainSingle();
        back![0].Text.Should().Be("Setup");
    }
}

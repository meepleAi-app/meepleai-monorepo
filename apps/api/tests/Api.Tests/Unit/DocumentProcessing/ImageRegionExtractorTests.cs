using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3447")]
public sealed class ImageRegionExtractorTests
{
    private const string HiResJson = """
    {"elements":[
      {"text":"Preparazione","page_number":1,"category":"Title","bbox":{"x":0.08,"y":0.10,"width":0.24,"height":0.05}},
      {"text":"","page_number":4,"category":"Image","bbox":{"x":0.10,"y":0.55,"width":0.80,"height":0.30}},
      {"text":"","page_number":5,"category":"FigureCaption","bbox":{"x":0.12,"y":0.20,"width":0.40,"height":0.06}},
      {"text":"","page_number":6,"category":"Image","bbox":null}
    ]}
    """;

    [Fact]
    public void FromHiResJson_KeepsImageAndFigureCaption_WithBbox_DropsOthers()
    {
        var regions = ImageRegionExtractor.FromHiResJson(HiResJson);

        regions.Should().HaveCount(2); // Image p4 + FigureCaption p5; Title dropped, bbox-null Image dropped
        regions.Should().ContainSingle(r => r.ElementType == "Image" && r.Page == 4 && r.Width == 0.80);
        regions.Should().ContainSingle(r => r.ElementType == "FigureCaption" && r.Page == 5);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json{")]
    [InlineData("{\"elements\":[]}")]
    public void FromHiResJson_NullEmptyInvalidOrNoElements_ReturnsEmpty(string? json)
    {
        ImageRegionExtractor.FromHiResJson(json).Should().BeEmpty();
    }

    [Fact]
    public void FromHiResJson_ClampsBboxToUnitRange()
    {
        var json = """{"elements":[{"text":"","page_number":2,"category":"Image","bbox":{"x":-0.1,"y":0.5,"width":1.5,"height":0.2}}]}""";
        var r = ImageRegionExtractor.FromHiResJson(json).Single();
        r.X.Should().Be(0.0);       // clamped from -0.1
        r.Width.Should().Be(1.0);   // clamped from 1.5
    }
}

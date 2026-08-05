using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3456")]
public sealed class ImageRegionExtractorTests
{
    private const string HiResJson = """
    {"elements":[
      {"text":"Preparazione","page_number":1,"category":"Title","bbox":{"x":0.08,"y":0.10,"width":0.24,"height":0.05}},
      {"text":"","page_number":4,"category":"Image","bbox":{"x":0.10,"y":0.55,"width":0.80,"height":0.30}},
      {"text":"","page_number":5,"category":"FigureCaption","bbox":{"x":0.12,"y":0.20,"width":0.40,"height":0.10}},
      {"text":"","page_number":6,"category":"Image","bbox":null},
      {"text":"","page_number":7,"category":"Image","bbox":{"x":0.10,"y":0.10,"width":0.05,"height":0.05}}
    ]}
    """;

    [Fact]
    public void FromHiResJson_KeepsLargeImageAndFigureCaption_DropsOtherTypesNullBboxAndTinyRegions()
    {
        var regions = ImageRegionExtractor.FromHiResJson(HiResJson);

        // Image p4 (0.80*0.30=0.24) + FigureCaption p5 (0.40*0.10=0.04) kept;
        // Title dropped (type), bbox-null Image p6 dropped, tiny Image p7 (0.05*0.05=0.0025) dropped (area < 3%).
        regions.Should().HaveCount(2);
        regions.Should().ContainSingle(r => r.ElementType == "Image" && r.Page == 4 && r.Width == 0.80);
        regions.Should().ContainSingle(r => r.ElementType == "FigureCaption" && r.Page == 5);
        regions.Should().NotContain(r => r.Page == 7);
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

    [Fact]
    public void FromHiResJson_DefaultThreshold_IsThreePercent()
    {
        ImageRegionExtractor.DefaultMinAreaFraction.Should().Be(0.03);
    }

    [Fact]
    public void FromHiResJson_DropsRegionsBelowMinArea()
    {
        // 0.10*0.20 = 0.02 (2%) < default 3% → dropped
        var json = """{"elements":[{"text":"","page_number":1,"category":"Image","bbox":{"x":0.1,"y":0.1,"width":0.10,"height":0.20}}]}""";
        ImageRegionExtractor.FromHiResJson(json).Should().BeEmpty();
    }

    [Fact]
    public void FromHiResJson_RespectsCustomMinArea()
    {
        // Image area = 0.20*0.20 = 0.04 (4%). Kept at default 3%, dropped at custom 10%.
        var json = """{"elements":[{"text":"","page_number":1,"category":"Image","bbox":{"x":0.1,"y":0.1,"width":0.20,"height":0.20}}]}""";
        ImageRegionExtractor.FromHiResJson(json).Should().HaveCount(1);
        ImageRegionExtractor.FromHiResJson(json, minAreaFraction: 0.10).Should().BeEmpty();
    }

    [Fact]
    public void FromHiResJson_AreaExactlyAtThreshold_IsKept()
    {
        // area = 0.30*0.10 = 0.03 == default threshold → kept (>=)
        var json = """{"elements":[{"text":"","page_number":1,"category":"Image","bbox":{"x":0.0,"y":0.0,"width":0.30,"height":0.10}}]}""";
        ImageRegionExtractor.FromHiResJson(json).Should().HaveCount(1);
    }

    [Fact]
    public void FromHiResJson_ZeroThreshold_DisablesFilter()
    {
        // tiny region kept when threshold is 0
        var json = """{"elements":[{"text":"","page_number":1,"category":"Image","bbox":{"x":0.1,"y":0.1,"width":0.01,"height":0.01}}]}""";
        ImageRegionExtractor.FromHiResJson(json, minAreaFraction: 0.0).Should().HaveCount(1);
    }

    /// <summary>
    /// Issue #3565: hi_res labels a graphics-drawn table as category "Table", not "Image".
    /// This is the real shape observed on wingspan_en_rulebook.pdf page 5 (the scorecard): the
    /// text is present but garbled because the row labels are rotated. Excluding "Table" meant the
    /// only genuine image-table of the rulebook never reached pdf_image_regions, so the VLM pass
    /// examined illustrations only and extracted nothing.
    /// </summary>
    [Fact]
    public void FromHiResJson_KeepsTableRegions()
    {
        var json = """
        {"elements":[
          {"text":"S Birds D R A C N Bonus cards O T N U O M End-of-round goals","page_number":5,"category":"Table","bbox":{"x":0.064,"y":0.769,"width":0.377,"height":0.187}}
        ]}
        """;

        var regions = ImageRegionExtractor.FromHiResJson(json);

        regions.Should().ContainSingle();
        regions[0].ElementType.Should().Be("Table");
        regions[0].Page.Should().Be(5);
        regions[0].Width.Should().BeApproximately(0.377, 1e-9);
    }

    [Fact]
    public void FromHiResJson_AppliesTheSameAreaAndBboxRulesToTableRegions()
    {
        var json = """
        {"elements":[
          {"text":"tiny","page_number":1,"category":"Table","bbox":{"x":0.1,"y":0.1,"width":0.05,"height":0.05}},
          {"text":"no bbox","page_number":2,"category":"Table","bbox":null}
        ]}
        """;

        // Table is not privileged: the anti-noise area floor and the bbox requirement still apply.
        ImageRegionExtractor.FromHiResJson(json).Should().BeEmpty();
    }
}

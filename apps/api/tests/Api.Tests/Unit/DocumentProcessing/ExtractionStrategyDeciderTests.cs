using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>
/// DC-2 (#3419): route re-extraction to hi_res only for PDFs whose prior extraction
/// (StructuredElementsJson) contains a Table element; otherwise fast.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3419")]
public sealed class ExtractionStrategyDeciderTests
{
    // Mirror the pipeline's serialization: default JsonSerializer over List<ExtractedElement>.
    private static string ElementsJson(params (string type, int page)[] els) =>
        JsonSerializer.Serialize(
            els.Select(e => new ExtractedElement($"{e.type} text", e.page, e.type)).ToList());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not valid json")]
    [InlineData("[]")]
    public void FromStructuredElements_NullEmptyOrInvalid_ReturnsFast(string? json)
    {
        ExtractionStrategyDecider.FromStructuredElements(json).Should().Be(ExtractionStrategy.Fast);
    }

    [Fact]
    public void FromStructuredElements_WithTableElement_ReturnsHiRes()
    {
        var json = ElementsJson(("Title", 1), ("NarrativeText", 1), ("Table", 2));
        ExtractionStrategyDecider.FromStructuredElements(json).Should().Be(ExtractionStrategy.HiRes);
    }

    [Fact]
    public void FromStructuredElements_NoTableElement_ReturnsFast()
    {
        var json = ElementsJson(("Title", 1), ("NarrativeText", 1), ("ListItem", 2));
        ExtractionStrategyDecider.FromStructuredElements(json).Should().Be(ExtractionStrategy.Fast);
    }

    [Theory]
    [InlineData(ExtractionStrategy.Fast, "fast")]
    [InlineData(ExtractionStrategy.HiRes, "hi_res")]
    public void ToWireString_MapsToUnstructuredTokens(ExtractionStrategy strategy, string expected)
    {
        strategy.ToWireString().Should().Be(expected);
    }
}

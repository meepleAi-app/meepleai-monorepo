using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Enums;

/// <summary>
/// Unit tests for DocumentCategory enum and IsAnalyzable() extension.
/// Issue #5443: Document classification for pipeline routing.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class DocumentCategoryTests
{
    [Theory]
    [InlineData(DocumentCategory.Rulebook)]
    [InlineData(DocumentCategory.Expansion)]
    [InlineData(DocumentCategory.Errata)]
    public void IsAnalyzable_WithAnalyzableCategories_ReturnsTrue(DocumentCategory category)
    {
        category.IsAnalyzable().Should().BeTrue();
    }

    [Theory]
    [InlineData(DocumentCategory.QuickStart)]
    [InlineData(DocumentCategory.Reference)]
    [InlineData(DocumentCategory.PlayerAid)]
    [InlineData(DocumentCategory.Other)]
    public void IsAnalyzable_WithNonAnalyzableCategories_ReturnsFalse(DocumentCategory category)
    {
        category.IsAnalyzable().Should().BeFalse();
    }

    [Fact]
    public void DocumentCategory_HasExpectedValues()
    {
        ((int)DocumentCategory.Rulebook).Should().Be(0);
        ((int)DocumentCategory.Expansion).Should().Be(1);
        ((int)DocumentCategory.Errata).Should().Be(2);
        ((int)DocumentCategory.QuickStart).Should().Be(3);
        ((int)DocumentCategory.Reference).Should().Be(4);
        ((int)DocumentCategory.PlayerAid).Should().Be(5);
        ((int)DocumentCategory.Other).Should().Be(6);
    }

    [Fact]
    public void DocumentCategory_DefaultValue_IsRulebook()
    {
        var defaultCategory = default(DocumentCategory);
        defaultCategory.Should().Be(DocumentCategory.Rulebook);
    }

    [Theory]
    [InlineData("Rulebook", DocumentCategory.Rulebook)]
    [InlineData("Expansion", DocumentCategory.Expansion)]
    [InlineData("Errata", DocumentCategory.Errata)]
    [InlineData("QuickStart", DocumentCategory.QuickStart)]
    [InlineData("Reference", DocumentCategory.Reference)]
    [InlineData("PlayerAid", DocumentCategory.PlayerAid)]
    [InlineData("Other", DocumentCategory.Other)]
    public void DocumentCategory_ParsesFromString(string input, DocumentCategory expected)
    {
        Enum.TryParse<DocumentCategory>(input, ignoreCase: true, out var result).Should().BeTrue();
        result.Should().Be(expected);
    }
}

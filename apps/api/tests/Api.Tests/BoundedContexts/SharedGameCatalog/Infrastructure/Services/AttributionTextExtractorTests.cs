using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Unit tests for <see cref="AttributionTextExtractor"/>.
///
/// Issue #2055 Phase G AC-G6 — DEC-G6-1 LOCKED: BE strips HTML upstream so the
/// FE renders attribution as plain text (no DOMPurify, no dangerouslySetInnerHTML).
/// These tests prove the strip handles all expected Wikimedia Commons shapes
/// PLUS the XSS attempt envelope.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "2055")]
public sealed class AttributionTextExtractorTests
{
    [Fact]
    public void Strip_PlainText_ReturnsAsIs()
    {
        var result = AttributionTextExtractor.Strip("Klaus Teuber");

        result.Should().Be("Klaus Teuber");
    }

    [Fact]
    public void Strip_AnchorTag_RemovesTagsKeepsText()
    {
        var result = AttributionTextExtractor.Strip("<a href=\"https://commons.wikimedia.org/wiki/User:KlausT\">Klaus Teuber</a>");

        result.Should().Be("Klaus Teuber");
    }

    [Fact]
    public void Strip_HtmlEntities_DecodesProperly()
    {
        var result = AttributionTextExtractor.Strip("Klaus &amp; Friends");

        result.Should().Be("Klaus & Friends");
    }

    [Fact]
    public void Strip_NestedTags_RemovesAllTags()
    {
        var result = AttributionTextExtractor.Strip("<div><span>Author</span></div>");

        result.Should().Be("Author");
    }

    [Fact]
    public void Strip_Null_ReturnsNull()
    {
        var result = AttributionTextExtractor.Strip(null);

        result.Should().BeNull();
    }

    [Fact]
    public void Strip_Empty_ReturnsNull()
    {
        var result = AttributionTextExtractor.Strip(string.Empty);

        result.Should().BeNull();
    }

    [Fact]
    public void Strip_WhitespaceOnly_ReturnsNull()
    {
        var result = AttributionTextExtractor.Strip("   \t\n  ");

        result.Should().BeNull();
    }

    [Fact]
    public void Strip_OnlyTags_ReturnsNull()
    {
        var result = AttributionTextExtractor.Strip("<br/><br/>");

        result.Should().BeNull();
    }

    [Fact]
    public void Strip_XssAttempt_StripsScriptTagButKeepsTextContent()
    {
        // Defence-in-depth: the BE strip is the FIRST line of defence per
        // DEC-G6-1. Combined with React's automatic text escaping (covered by
        // a parallel FE test) this guarantees no JS execution path.
        var result = AttributionTextExtractor.Strip("<script>alert(1)</script>Klaus");

        result.Should().Be("alert(1) Klaus");
    }
}

using Api.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.Services;

/// <summary>
/// Wave 3 Phase 3 (Issue #805 / PR #732 §6.3.3) — markdown subset enforcement
/// tests for <see cref="MarkdownSubsetSanitizer"/>. Validates each rule:
/// H4-H6 demote, image replacement, raw HTML strip, footnote strip.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class MarkdownSubsetSanitizerTests
{
    [Fact]
    public void Sanitize_NullInput_ReturnsEmptyString()
    {
        MarkdownSubsetSanitizer.Sanitize(null).Should().Be(string.Empty);
    }

    [Fact]
    public void Sanitize_WhitespaceInput_ReturnsAsIs()
    {
        MarkdownSubsetSanitizer.Sanitize("   ").Should().Be("   ");
    }

    [Theory]
    [InlineData("#### Subheader", "**Subheader**")]
    [InlineData("##### Deep", "**Deep**")]
    [InlineData("###### Deeper", "**Deeper**")]
    public void Sanitize_H4toH6_DemotedToBold(string input, string expected)
    {
        MarkdownSubsetSanitizer.Sanitize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("# H1")]
    [InlineData("## H2")]
    [InlineData("### H3")]
    public void Sanitize_H1toH3_Preserved(string input)
    {
        MarkdownSubsetSanitizer.Sanitize(input).Should().Be(input);
    }

    [Fact]
    public void Sanitize_Image_ReplacedWithAltPlaceholder()
    {
        var input = "Some text ![Eagle photo](http://example.com/eagle.png) more text.";
        var result = MarkdownSubsetSanitizer.Sanitize(input);

        result.Should().Contain("[Image: Eagle photo]");
        result.Should().NotContain("![Eagle photo]");
        result.Should().NotContain("eagle.png");
    }

    [Fact]
    public void Sanitize_ImageWithEmptyAlt_FallsBackToBareLabel()
    {
        var input = "![](http://example.com/x.png)";
        MarkdownSubsetSanitizer.Sanitize(input).Should().Contain("[Image]");
    }

    [Fact]
    public void Sanitize_RawHtml_Stripped()
    {
        var input = "Hello <script>alert(1)</script> world <iframe src=\"x\"></iframe>!";
        var result = MarkdownSubsetSanitizer.Sanitize(input);

        result.Should().NotContain("<script>");
        result.Should().NotContain("</script>");
        result.Should().NotContain("<iframe");
        result.Should().NotContain("</iframe>");
        result.Should().Contain("alert(1)"); // text content kept
    }

    [Fact]
    public void Sanitize_FootnoteMarker_Stripped()
    {
        var input = "Reference[^1] in body text.";
        var result = MarkdownSubsetSanitizer.Sanitize(input);

        result.Should().Contain("Reference in body text.");
        result.Should().NotContain("[^1]");
    }

    [Fact]
    public void Sanitize_FootnoteDefinition_Stripped()
    {
        var input = "Body text\n[^1]: Footnote definition content.\nMore body.";
        var result = MarkdownSubsetSanitizer.Sanitize(input);

        result.Should().NotContain("[^1]");
        result.Should().NotContain("Footnote definition content");
        result.Should().Contain("Body text");
        result.Should().Contain("More body");
    }

    [Fact]
    public void Sanitize_PreservesFencedCodeBlock()
    {
        var input = "```python\nprint('hi')\n```";
        MarkdownSubsetSanitizer.Sanitize(input).Should().Be(input);
    }

    [Fact]
    public void Sanitize_PreservesInlineCode()
    {
        var input = "Use `git status` to check.";
        MarkdownSubsetSanitizer.Sanitize(input).Should().Be(input);
    }

    [Fact]
    public void Sanitize_PreservesPipeTable()
    {
        var input = "| a | b |\n|---|---|\n| 1 | 2 |";
        MarkdownSubsetSanitizer.Sanitize(input).Should().Be(input);
    }

    [Fact]
    public void Sanitize_PreservesBlockquote()
    {
        var input = "> Quoted text.";
        MarkdownSubsetSanitizer.Sanitize(input).Should().Be(input);
    }

    [Fact]
    public void Sanitize_PreservesEmphasis()
    {
        var input = "This is **bold** and *italic* and _underline_.";
        MarkdownSubsetSanitizer.Sanitize(input).Should().Be(input);
    }

    [Fact]
    public void Sanitize_CombinedRules_AppliedInOrder()
    {
        var input = "#### Header[^1]\n\n[^1]: Footnote\n\n![](pic.png) <b>bold</b>";
        var result = MarkdownSubsetSanitizer.Sanitize(input);

        result.Should().Contain("**Header**");
        result.Should().NotContain("[^1]");
        result.Should().Contain("[Image]");
        result.Should().NotContain("<b>");
        result.Should().Contain("bold"); // tag stripped, text kept
    }
}

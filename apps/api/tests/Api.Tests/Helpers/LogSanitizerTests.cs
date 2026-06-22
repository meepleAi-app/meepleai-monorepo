using Api.Helpers;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Api.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="LogSanitizer"/> — canonical log-forging sanitizer (ADR-058, issue #1181).
/// Validates control-character stripping and URL-decode behavior for request paths.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class LogSanitizerTests
{
    [Fact]
    public void Sanitize_NullInput_ReturnsEmpty()
    {
        LogSanitizer.Sanitize(null).Should().BeEmpty();
    }

    [Fact]
    public void Sanitize_EmptyInput_ReturnsEmpty()
    {
        LogSanitizer.Sanitize(string.Empty).Should().BeEmpty();
    }

    [Theory]
    [InlineData("plain text", "plain text")]
    [InlineData("line1\nline2", "line1line2")]
    [InlineData("line1\r\nline2", "line1line2")]
    [InlineData("col1\tcol2", "col1 col2")]
    [InlineData("mixed\r\n\ttab", "mixed tab")]
    public void Sanitize_StripsControlCharacters(string input, string expected)
    {
        LogSanitizer.Sanitize(input).Should().Be(expected);
    }

    [Fact]
    public void SanitizeWithMaxLength_BelowLimit_ReturnsUnchanged()
    {
        LogSanitizer.Sanitize("short", maxLength: 100).Should().Be("short");
    }

    [Fact]
    public void SanitizeWithMaxLength_AboveLimit_TruncatesAndAppendsEllipsis()
    {
        LogSanitizer.Sanitize("0123456789", maxLength: 4).Should().Be("0123...");
    }

    [Fact]
    public void SanitizeWithMaxLength_StripsControlCharsBeforeMeasuring()
    {
        // "ab\ncd\nef" → "abcdef" (length 6) → fits within maxLength=10
        LogSanitizer.Sanitize("ab\ncd\nef", maxLength: 10).Should().Be("abcdef");
    }

    [Fact]
    public void SanitizePath_PlainPath_ReturnsUnchanged()
    {
        var path = new PathString("/api/games");
        LogSanitizer.SanitizePath(path).Should().Be("/api/games");
    }

    [Fact]
    public void SanitizePath_LiteralControlChars_StripsThem()
    {
        var path = new PathString("/api\rgames\nstuff");
        LogSanitizer.SanitizePath(path).Should().Be("/apigamesstuff");
    }

    [Fact]
    public void SanitizePath_UrlEncodedCarriageReturn_DecodedAndStripped()
    {
        // %0D = \r. URL-decode first, then strip — log-forging defense in depth.
        var path = new PathString("/api%0Dnew-log-line");
        LogSanitizer.SanitizePath(path).Should().Be("/apinew-log-line");
    }

    [Fact]
    public void SanitizePath_UrlEncodedLineFeed_DecodedAndStripped()
    {
        // %0A = \n
        var path = new PathString("/api%0Anew-log-line");
        LogSanitizer.SanitizePath(path).Should().Be("/apinew-log-line");
    }

    [Fact]
    public void SanitizePath_UrlEncodedCrLf_DecodedAndStripped()
    {
        // %0D%0A = \r\n — classic CRLF injection vector
        var path = new PathString("/api%0D%0Aforged-entry");
        LogSanitizer.SanitizePath(path).Should().Be("/apiforged-entry");
    }

    [Fact]
    public void SanitizePath_EmptyPath_ReturnsEmpty()
    {
        LogSanitizer.SanitizePath(new PathString(string.Empty)).Should().BeEmpty();
    }
}

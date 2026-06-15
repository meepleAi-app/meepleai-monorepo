using Api.SharedKernel.Application.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedKernel.Application.Services;

[Trait("Category", "Unit")]
public class SlugifierTests
{
    [Theory]
    [InlineData("Ark Nova", "ark-nova")]
    [InlineData("7 Wonders Duel", "7-wonders-duel")]
    [InlineData("King's Dilemma", "kings-dilemma")]
    [InlineData("Ticket to Ride: Europe", "ticket-to-ride-europe")]
    [InlineData("CATAN", "catan")]
    [InlineData("Wingspan", "wingspan")]
    public void Slugify_HappyPath_ReturnsKebabCase(string input, string expected)
    {
        var result = Slugifier.Slugify(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Slugify_EmptyOrWhitespace_ReturnsUnknown(string? input)
    {
        var result = Slugifier.Slugify(input!);
        result.Should().Be("unknown");
    }

    [Theory]
    [InlineData("Café Royal", "cafe-royal")]
    [InlineData("Pingüino", "pinguino")]
    [InlineData("Über Bahn", "ber-bahn")]
    public void Slugify_DiacriticsAndAccents_StripsToAscii(string input, string expected)
    {
        var result = Slugifier.Slugify(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("!!! ???", "unknown")]
    [InlineData("...", "unknown")]
    [InlineData("---", "unknown")]
    public void Slugify_OnlySpecialChars_ReturnsUnknown(string input, string expected)
    {
        var result = Slugifier.Slugify(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void Slugify_MultipleSpaces_CollapsesToSingleHyphen()
    {
        var result = Slugifier.Slugify("Game    With     Many   Spaces");
        result.Should().Be("game-with-many-spaces");
    }

    [Fact]
    public void Slugify_LeadingTrailingWhitespace_Trims()
    {
        var result = Slugifier.Slugify("  Hello World  ");
        result.Should().Be("hello-world");
    }
}

using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

[Trait("Category", TestCategories.Unit)]
public class BagOfWordsKeywordExtractorTests
{
    private readonly IKeywordExtractor _sut = new BagOfWordsKeywordExtractor();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Extract_NullOrWhitespace_ReturnsEmpty(string? input)
    {
        var result = _sut.Extract(input!);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Extract_Lowercases_Tokens()
    {
        var result = _sut.Extract("Roll The Dice");

        result.Should().Contain("roll");
        result.Should().Contain("dice");
        result.Should().NotContain("Roll");
        result.Should().NotContain("Dice");
        result.Should().NotContain("the"); // stopword
        result.Should().NotContain("The");
    }

    [Fact]
    public void Extract_StripsPunctuation()
    {
        var result = _sut.Extract("Place, on the board! End.");

        result.Should().Contain("place");
        result.Should().Contain("board");
        result.Should().Contain("end");
        result.Should().OnlyContain(t => !t.Contains(',', StringComparison.Ordinal));
        result.Should().OnlyContain(t => !t.Contains('!', StringComparison.Ordinal));
        result.Should().OnlyContain(t => !t.Contains('.', StringComparison.Ordinal));
    }

    [Fact]
    public void Extract_FiltersTooShortTokens()
    {
        // "at" and "to" are stopwords; "go" is len=2 -> short
        var result = _sut.Extract("at to go");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Extract_RemovesStopwords()
    {
        // Mixed EN (the, is, a, with) + IT (il, della, con) stopwords; "castle" and "torre" are content.
        var result = _sut.Extract("the castle is a with il della con torre");

        result.Should().NotContain("the");
        result.Should().NotContain("is");
        result.Should().NotContain("with");
        result.Should().NotContain("il");
        result.Should().NotContain("della");
        result.Should().NotContain("con");
        result.Should().Contain("castle");
        result.Should().Contain("torre");
    }

    [Fact]
    public void Extract_DeduplicatesTokens()
    {
        var result = _sut.Extract("dice dice dice");

        result.Should().ContainSingle().Which.Should().Be("dice");
    }

    [Fact]
    public void Extract_IsDeterministicallySorted()
    {
        var result = _sut.Extract("zebra apple mango");

        result.Should().Equal("apple", "mango", "zebra");
    }

    [Fact]
    public void Extract_SupportsItalianText()
    {
        var result = _sut.Extract("Lancia il dado sulla plancia");

        result.Should().Contain("dado");
        result.Should().Contain("lancia");
        result.Should().Contain("plancia");
        result.Should().NotContain("il");
        result.Should().NotContain("sulla");
    }

    [Fact]
    public void Extract_HandlesMixedLanguages()
    {
        // EN stopwords: the, with; IT stopwords: della, con. Content: players, dice, resources, pedine.
        var result = _sut.Extract("The players with della dice roll con resources pedine");

        result.Should().NotContain("the");
        result.Should().NotContain("with");
        result.Should().NotContain("della");
        result.Should().NotContain("con");
        result.Should().Contain("players");
        result.Should().Contain("dice");
        result.Should().Contain("roll");
        result.Should().Contain("resources");
        result.Should().Contain("pedine");
    }

    [Fact]
    public void Extract_RetainsDigitsInTokens()
    {
        // Defensive: digits should pass through. "d20" (len=3) stays; single digits get merged out by length filter.
        var result = _sut.Extract("roll d20 dice");

        result.Should().Contain("d20");
        result.Should().Contain("roll");
        result.Should().Contain("dice");
    }
}

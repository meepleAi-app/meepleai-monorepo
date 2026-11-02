using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// #4782 — the page map of an extracted text. It is stored next to the text and used again at re-index, so what
/// matters is that it is refused whenever it no longer describes the text it is next to.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PageSpansTests
{
    // "alpha" (0..5) + "\n\n" + "beta" (7..11) + "\n\n" + "gamma" (13..18)
    private const string Text = "alpha\n\nbeta\n\ngamma";

    private static readonly PageSpan[] Spans = [new(1, 0), new(2, 7), new(4, 13)];

    [Fact]
    public void IsConsistentWith_AMapBuiltFromTheTextItDescribes_IsAccepted()
    {
        PageSpans.IsConsistentWith(Spans, Text).Should().BeTrue();
    }

    [Fact]
    public void IsConsistentWith_NoMapOrNoText_IsRefused()
    {
        PageSpans.IsConsistentWith(null, Text).Should().BeFalse();
        PageSpans.IsConsistentWith([], Text).Should().BeFalse();
        PageSpans.IsConsistentWith(Spans, null).Should().BeFalse();
        PageSpans.IsConsistentWith(Spans, "").Should().BeFalse();
    }

    [Fact]
    public void IsConsistentWith_AFirstPageThatDoesNotStartAtTheBeginning_IsRefused()
    {
        PageSpans.IsConsistentWith([new(1, 2), new(2, 7), new(4, 13)], Text).Should().BeFalse();
    }

    [Fact]
    public void IsConsistentWith_AStartPastTheEndOfTheText_IsRefused()
    {
        // The text was shortened after the map was written.
        PageSpans.IsConsistentWith(Spans, "alpha\n\nbeta").Should().BeFalse();
    }

    [Fact]
    public void IsConsistentWith_ALastPageThatWouldBeEmpty_IsRefused()
    {
        // The second page starts exactly where the text ends.
        PageSpans.IsConsistentWith([new(1, 0), new(2, 7)], "alpha\n\n").Should().BeFalse();
    }

    [Fact]
    public void IsConsistentWith_AStartThatIsNotPrecededByTheSeparator_IsRefused()
    {
        // Same length, other text: the offsets still fall inside it but no longer on a page break.
        PageSpans.IsConsistentWith(Spans, "alpha-\nbeta\n\ngamma").Should().BeFalse();
    }

    [Fact]
    public void IsConsistentWith_ThePageBeforeAStartMustHoldAtLeastOneCharacter()
    {
        // Starts 0 and 2: the first page would be empty (the separator itself).
        PageSpans.IsConsistentWith([new(1, 0), new(2, 2)], "\n\nbeta").Should().BeFalse();
    }

    [Fact]
    public void IsConsistentWith_PagesThatGoBackOrBelowOne_AreRefused()
    {
        PageSpans.IsConsistentWith([new(3, 0), new(2, 7), new(4, 13)], Text).Should().BeFalse();
        PageSpans.IsConsistentWith([new(0, 0), new(2, 7), new(4, 13)], Text).Should().BeFalse();
    }

    [Fact]
    public void IsConsistentWith_TwoSpansOnTheSamePage_AreAccepted()
    {
        // The page-level splitter of the SmolDocling stage cuts a dense page into several parts that keep its number.
        PageSpans.IsConsistentWith([new(1, 0), new(1, 7), new(2, 13)], Text).Should().BeTrue();
    }

    [Fact]
    public void IsConsistentWith_ANegativeStart_IsRefused_NotAnException()
    {
        // A corrupted jsonb value: the arithmetic on the start would wrap around and the slice would throw.
        PageSpans.IsConsistentWith([new(1, 0), new(1, int.MinValue)], Text).Should().BeFalse();
        PageSpans.IsConsistentWith([new(1, 0), new(2, -5)], Text).Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 1)] // first character of the first page
    [InlineData(4, 1)] // last character of the first page
    [InlineData(5, 1)] // inside the blank line after it: still the page before
    [InlineData(6, 1)]
    [InlineData(7, 2)] // first character of the second page
    [InlineData(10, 2)]
    [InlineData(13, 4)] // first character of the last page (page 3 had no text)
    [InlineData(18, 4)] // past the end: the last page
    public void PageAt_IsThePageOfTheLastSpanStartingAtOrBeforeTheOffset(int offset, int expectedPage)
    {
        PageSpans.PageAt(Spans, offset).Should().Be(expectedPage);
    }

    [Fact]
    public void PageAt_AnOffsetBeforeTheFirstSpan_GetsTheFirstPage()
    {
        PageSpans.PageAt([new(3, 10), new(5, 20)], 2).Should().Be(3);
    }

    [Fact]
    public void PageAt_PartsOfTheSamePage_GiveThatPage()
    {
        PageSpans.PageAt([new(1, 0), new(1, 7), new(2, 13)], 9).Should().Be(1);
    }

    [Fact]
    public void SerializeThenParse_GivesTheSameMap()
    {
        var json = PageSpans.Serialize(Spans);

        json.Should().Be("[[1,0],[2,7],[4,13]]");
        PageSpans.Parse(json).Should().Equal(Spans);
    }

    [Fact]
    public void Parse_ReadsTheMapAsPostgresJsonbReturnsIt_WithSpacesAfterTheCommas()
    {
        PageSpans.Parse("[[1, 0], [2, 7], [4, 13]]").Should().Equal(Spans);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("[[1,0],null]")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{\"page\":1}")]
    [InlineData("[[1]]")]
    [InlineData("[[1,0,5]]")]
    [InlineData("[1,2]")]
    public void Parse_AnythingThatIsNotAListOfPairs_GivesNoMap(string? json)
    {
        PageSpans.Parse(json).Should().BeNull();
    }
}

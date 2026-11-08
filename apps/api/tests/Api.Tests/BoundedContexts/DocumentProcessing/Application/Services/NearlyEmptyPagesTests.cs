using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// #4437 — Docnet does not OCR. The pages an extraction returns without text are the only trace of a scan, so what counts
/// as «nearly empty» is pinned here, boundary included.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class NearlyEmptyPagesTests
{
    private static PageTextChunk Page(int number, string text, bool estimated = false) =>
        new(number, text, 0, text.Length, estimated);

    private static string Chars(int count) => new('a', count);

    [Fact]
    public void Count_CountsThePagesBelowTheThreshold_AndOnlyThose()
    {
        PageTextChunk[] pages = [Page(1, Chars(400)), Page(2, ""), Page(3, Chars(NearlyEmptyPages.MaxCharacters - 1)), Page(4, Chars(250))];

        NearlyEmptyPages.Count(pages).Should().Be(2, "pages 2 and 3 are under the threshold; 1 and 4 are not");
    }

    [Fact]
    public void Count_ThePageExactlyAtTheThreshold_IsNotNearlyEmpty()
    {
        NearlyEmptyPages.Count([Page(1, Chars(NearlyEmptyPages.MaxCharacters))]).Should().Be(0);
        NearlyEmptyPages.Count([Page(1, Chars(NearlyEmptyPages.MaxCharacters - 1))]).Should().Be(1);
    }

    [Fact]
    public void Count_BlankSpace_DoesNotCountAsText()
    {
        var padded = new string(' ', 300) + "ab" + new string('\n', 300);

        NearlyEmptyPages.Count([Page(1, padded)]).Should().Be(1, "300 spaces around two letters is still a page with two characters of text");
    }

    [Fact]
    public void Count_EstimatedPages_AreNotPagesOfThePdf_SoNothingIsCounted()
    {
        PageTextChunk[] pages = [Page(1, Chars(400), estimated: true), Page(2, "short", estimated: true), Page(3, "", estimated: true)];

        NearlyEmptyPages.Count(pages).Should().Be(0, "equal slices of the whole text say nothing about which page of the PDF is empty");
    }

    [Fact]
    public void Count_NoPages_IsZero()
    {
        NearlyEmptyPages.Count([]).Should().Be(0);
    }

    [Fact]
    public void NoPageHasText_EveryPageEmpty_IsTrue()
    {
        NearlyEmptyPages.NoPageHasText([Page(1, ""), Page(2, "   \n "), Page(3, "")]).Should().BeTrue();
    }

    [Fact]
    public void NoPageHasText_OnePageWithText_IsFalse_EvenIfItIsNearlyEmpty()
    {
        NearlyEmptyPages.NoPageHasText([Page(1, ""), Page(2, "x"), Page(3, "")]).Should().BeFalse(
            "a page with one character is nearly empty but not a document without text");
    }

    [Fact]
    public void NoPageHasText_NoPagesAtAll_IsFalse()
    {
        NearlyEmptyPages.NoPageHasText([]).Should().BeFalse("an extraction that found no pages is a different failure, not a scan");
    }
}

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

    private static PagedTextExtractionResult Extraction(bool textLayerOnly, params PageTextChunk[] pages) =>
        PagedTextExtractionResult.CreateSuccess(pages, pages.Length, pages.Sum(p => p.CharCount), ocrTriggered: false)
        with { TextLayerOnly = textLayerOnly };

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
    public void CountIn_CountsOnlyWhenTheExtractorReadsTheTextLayerAlone()
    {
        PageTextChunk[] pages = [Page(1, Chars(400)), Page(2, ""), Page(3, "short")];

        NearlyEmptyPages.CountIn(Extraction(textLayerOnly: true, pages)).Should().Be(2);
        NearlyEmptyPages.CountIn(Extraction(textLayerOnly: false, pages)).Should().Be(
            0, "an extractor that reads pixels, or does not know its pages, says nothing about a scan");
    }

    [Fact]
    public void NoTextAnywhere_NeedsTheTextLayerMark_NotJustEmptyPages()
    {
        PageTextChunk[] pages = [Page(1, ""), Page(2, "  ")];

        NearlyEmptyPages.NoTextAnywhere(Extraction(textLayerOnly: true, pages)).Should().BeTrue();
        NearlyEmptyPages.NoTextAnywhere(Extraction(textLayerOnly: false, pages)).Should().BeFalse(
            "a VLM or an OCR extractor that returns nothing is not proof of a scan, and «no OCR» would be false for it");
        NearlyEmptyPages.NoTextAnywhere(Extraction(textLayerOnly: true, [Page(1, "x")])).Should().BeFalse();
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

    /// <summary>One page per character: <c>T</c> = a page of text, <c>.</c> = a page with no text.</summary>
    private static PageTextChunk[] Pages(string pattern) =>
        pattern.Select((c, i) => Page(i + 1, c == 'T' ? Chars(300) : "")).ToArray();

    // #4931 — the rule, with the cases the issue asks for. The numbers come from NearlyEmptyPagesCorpusMeasurement on the
    // local corpus: covers (one page, first or last) are the common case, the scans are the six documents above 90%.
    [Theory]
    [InlineData("..........", true, "a full scan")]
    [InlineData("T.........", true, "a scan with one page of text (nine of ten empty)")]
    [InlineData("T.T.", true, "exactly half of the pages, and two of them")]
    [InlineData("TTT..", false, "two of five is 40%, below half")]
    [InlineData(".TTTTTTTTT", false, "a cover and the rest text: one page")]
    [InlineData("TTTTTTTTT.", false, "a blank back and the rest text: one page")]
    [InlineData(".T", false, "one of two pages is 50% but a single page: a cover alone never flags")]
    [InlineData("TTTTTTTTTT", false, "a document made only of text")]
    [InlineData("..", true, "two pages, both empty: the floor is two pages, not three")]
    public void Assess_FlagsOnlyADocumentWithAtLeastHalfItsPagesNearlyEmpty_AndAtLeastTwo(string pattern, bool flagged, string because)
    {
        var assessment = NearlyEmptyPages.Assess(Extraction(textLayerOnly: true, Pages(pattern)));

        assessment.TotalPages.Should().Be(pattern.Length);
        assessment.NearlyEmpty.Should().Be(pattern.Count(c => c == '.'));
        assessment.Flagged.Should().Be(flagged, because);
    }

    [Fact]
    public void Assess_WithEstimatedPages_NeverFlags_BecauseTheSlicesAreNotThePagesOfThePdf()
    {
        var pages = Pages("....").Select(p => p with { PageIsEstimated = true }).ToArray();

        var assessment = NearlyEmptyPages.Assess(Extraction(textLayerOnly: true, pages));

        assessment.NearlyEmpty.Should().Be(0);
        assessment.Flagged.Should().BeFalse("equal slices of the whole text say nothing about the document");
    }

    [Fact]
    public void Assess_WhenTheExtractorIsNotTextLayerOnly_NeverFlags()
    {
        NearlyEmptyPages.Assess(Extraction(textLayerOnly: false, Pages("...."))).Flagged.Should().BeFalse(
            "an extractor that reads pixels, or does not know its pages, says nothing about a scan");
    }

    [Fact]
    public void Assess_WithNoPages_IsNotFlagged_AndHasFractionZero()
    {
        var assessment = NearlyEmptyPages.Assess(Extraction(textLayerOnly: true));

        assessment.Flagged.Should().BeFalse();
        assessment.Fraction.Should().Be(0d);
    }
}

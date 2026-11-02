using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// #4782 — the text a pipeline stores and the page map next to it are built in one loop, so they cannot disagree.
/// The map is withheld whenever the page numbers are not the pages of the PDF.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PageTextJoinerTests
{
    private static PageTextChunk Page(int number, string text, bool estimated = false) =>
        new(number, text, 0, text.Length, estimated);

    [Fact]
    public void Join_TextIsTheNonEmptyPagesJoinedByABlankLine_AsTheThreePipelinesDidByHand()
    {
        PageTextChunk[] pages = [Page(1, "one"), Page(2, "   "), Page(3, "three"), Page(4, ""), Page(5, "five")];

        var joined = PageTextJoiner.Join(pages);

        joined.Text.Should().Be(string.Join("\n\n", pages.Where(p => !p.IsEmpty).Select(p => p.Text)));
        joined.Text.Should().Be("one\n\nthree\n\nfive");
    }

    [Fact]
    public void Join_TheMapStartsEachNonEmptyPageWhereItsTextBegins()
    {
        var joined = PageTextJoiner.Join([Page(1, "one"), Page(2, ""), Page(3, "three"), Page(5, "five")]);

        joined.Spans.Should().Equal(new PageSpan(1, 0), new PageSpan(3, 5), new PageSpan(5, 12));
        joined.Spans.Should().NotBeNull();
        PageSpans.IsConsistentWith(joined.Spans, joined.Text).Should().BeTrue();
        foreach (var span in joined.Spans!)
        {
            joined.Text[span.Start..].Should().StartWith(span.Page switch { 1 => "one", 3 => "three", _ => "five" });
        }
    }

    [Fact]
    public void Join_APageWhoseNumberIsAnEstimate_WithholdsTheMap_ButNotTheText()
    {
        // The Unstructured extractor cuts the text into equal parts and numbers them 1..N.
        var joined = PageTextJoiner.Join([Page(1, "one", estimated: true), Page(2, "two", estimated: true)]);

        joined.Text.Should().Be("one\n\ntwo");
        joined.Spans.Should().BeNull();
    }

    [Fact]
    public void Join_OneEstimatedPageAmongMeasuredOnes_WithholdsTheWholeMap()
    {
        var joined = PageTextJoiner.Join([Page(1, "one"), Page(2, "two", estimated: true), Page(3, "three")]);

        joined.Spans.Should().BeNull();
    }

    [Fact]
    public void Join_PagesThatGoBackInTheTextOrder_WithholdTheMap()
    {
        PageTextJoiner.Join([Page(3, "three"), Page(1, "one")]).Spans.Should().BeNull();
    }

    [Fact]
    public void Join_ThePageNumberZeroOrBelow_WithholdsTheMap()
    {
        PageTextJoiner.Join([Page(0, "zero"), Page(1, "one")]).Spans.Should().BeNull();
        PageTextJoiner.Join([Page(-1, "minus")]).Spans.Should().BeNull();
    }

    [Fact]
    public void Join_PartsOfTheSamePage_KeepTheirNumberInTheMap()
    {
        var joined = PageTextJoiner.Join([Page(1, "one-a"), Page(1, "one-b"), Page(2, "two")]);

        joined.Spans.Should().Equal(new PageSpan(1, 0), new PageSpan(1, 7), new PageSpan(2, 14));
    }

    [Fact]
    public void Join_NoPageWithText_GivesAnEmptyTextAndNoMap()
    {
        var joined = PageTextJoiner.Join([Page(1, " "), Page(2, "")]);

        joined.Text.Should().BeEmpty();
        joined.Spans.Should().BeNull();
    }
}

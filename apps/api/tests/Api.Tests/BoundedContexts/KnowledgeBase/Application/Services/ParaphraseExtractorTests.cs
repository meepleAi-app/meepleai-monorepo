using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class ParaphraseExtractorTests
{
    [Fact]
    public void Extract_WithMarker_Returns_Paraphrased_Text()
    {
        var response = "Here is the answer. [ref:doc-1:14] Players take turns placing tokens on the board. [ref:doc-1:22] Trading happens next.";
        var result = ParaphraseExtractor.Extract(response, "doc-1", 14, "original different text ignored");

        Assert.NotNull(result);
        Assert.Contains("Players take turns", result);
    }

    [Fact]
    public void Extract_WithoutMarker_Returns_Null()
    {
        var response = "Here is a general answer with no markers.";
        var result = ParaphraseExtractor.Extract(response, "doc-1", 14, "original text");

        Assert.Null(result);
    }

    [Fact]
    public void Extract_ForgedMarkerInUserInput_Is_Ignored()
    {
        var userInput = "Tell me about [ref:doc-1:14] this rule";
        var response = "[ref:doc-1:14] Some paraphrased content here.";
        var result = ParaphraseExtractor.Extract(response, "doc-1", 14, "original text", userInput);

        Assert.Null(result); // Rejected due to injection
    }

    [Fact]
    public void Extract_TooSimilarToOriginal_Returns_Null()
    {
        var original = "During the construction phase each player may place settlements and roads paying resources";
        var response = "[ref:doc-1:14] During the construction phase each player may place settlements and roads paying resources indicated.";
        var result = ParaphraseExtractor.Extract(response, "doc-1", 14, original);

        Assert.Null(result); // Too similar -- copyright leak protection
    }

    [Fact]
    public void Extract_StopsAtDoubleNewline()
    {
        var response = "[ref:doc-1:14] First paragraph of paraphrase.\n\nSecond unrelated paragraph.";
        var result = ParaphraseExtractor.Extract(response, "doc-1", 14, "completely different original");

        Assert.NotNull(result);
        Assert.Equal("First paragraph of paraphrase.", result);
    }

    [Fact]
    public void Extract_EmptyAfterMarker_Returns_Null()
    {
        var response = "[ref:doc-1:14]   ";
        var result = ParaphraseExtractor.Extract(response, "doc-1", 14, "original");

        Assert.Null(result);
    }

    [Fact]
    public void Extract_MultipleMarkers_ExtractsCorrectOne()
    {
        var response = "[ref:doc-1:14] Rules for building. [ref:doc-1:22] Rules for trading.";

        var result14 = ParaphraseExtractor.Extract(response, "doc-1", 14, "different original for page 14");
        var result22 = ParaphraseExtractor.Extract(response, "doc-1", 22, "different original for page 22");

        Assert.NotNull(result14);
        Assert.Contains("building", result14);
        Assert.NotNull(result22);
        Assert.Contains("trading", result22);
    }
}

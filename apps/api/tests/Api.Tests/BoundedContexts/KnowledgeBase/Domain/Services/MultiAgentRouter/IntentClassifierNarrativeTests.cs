using Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;

[Trait("Category", TestCategories.Unit)]
public class IntentClassifierNarrativeTests
{
    private readonly IntentClassifier _sut = new();

    [Theory]
    [InlineData("tell me about the lore of this game")]
    [InlineData("racconta la storia del gioco")]
    [InlineData("describe the atmosphere and setting")]
    [InlineData("who are the characters in this game")]
    [InlineData("ambientazione del gioco")]
    public void ClassifyQuery_NarrativeQueries_ReturnsNarrativeIntent(string query)
    {
        var result = _sut.ClassifyQuery(query);
        result.Intent.Should().Be(AgentIntent.Narrative);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.40);
    }

    [Theory]
    [InlineData("what is the best strategy")]
    [InlineData("is this move legal")]
    [InlineData("how to play this game")]
    public void ClassifyQuery_NonNarrativeQueries_DoesNotReturnNarrative(string query)
    {
        var result = _sut.ClassifyQuery(query);
        result.Intent.Should().NotBe(AgentIntent.Narrative);
    }
}

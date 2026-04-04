using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase;

internal sealed class QueryComplexityAnalyzerTests
{
    private readonly QueryComplexityAnalyzer _analyzer = new();

    [Theory]
    [InlineData("Quanti giocatori?", QueryRoutingTier.Low)]
    [InlineData("Come si vince?", QueryRoutingTier.Low)]
    [InlineData("Spiega il meccanismo di trading in Catan", QueryRoutingTier.Medium)]
    [InlineData("Qual è la differenza strategica tra costruire strade e insediamenti nel early game?", QueryRoutingTier.High)]
    [InlineData("Confronta le strategie ottimali di espansione tra Catan base e la variante 5-6 giocatori", QueryRoutingTier.High)]
    public void Analyze_ReturnsCorrectComplexity(string query, QueryRoutingTier expected)
    {
        var result = _analyzer.Analyze(query);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Analyze_LongQuery_UpgradesComplexity()
    {
        var longQuery = new string('x', 201); // > 200 chars
        var result = _analyzer.Analyze(longQuery);
        Assert.True(result >= QueryRoutingTier.Medium);
    }
}

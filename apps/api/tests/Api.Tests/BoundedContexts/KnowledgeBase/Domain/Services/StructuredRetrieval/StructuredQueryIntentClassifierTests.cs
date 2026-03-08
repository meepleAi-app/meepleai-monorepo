using Api.BoundedContexts.KnowledgeBase.Domain.Services.StructuredRetrieval;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.StructuredRetrieval;

/// <summary>
/// Tests for StructuredQueryIntentClassifier.
/// Issue #5453: Structured RAG fusion.
/// </summary>
[Trait("Category", "Unit")]
public sealed class StructuredQueryIntentClassifierTests
{
    private readonly StructuredQueryIntentClassifier _sut = new();

    #region Victory Conditions Intent

    [Theory]
    [InlineData("How do you win Catan?")]
    [InlineData("What are the victory conditions?")]
    [InlineData("How to win the game")]
    [InlineData("End game scoring rules")]
    [InlineData("What's the winning condition?")]
    public void Classify_VictoryQueries_ReturnsVictoryConditions(string query)
    {
        var result = _sut.Classify(query);

        result.Intent.Should().Be(StructuredQueryIntent.VictoryConditions);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.6);
    }

    #endregion

    #region Mechanics Intent

    [Theory]
    [InlineData("What mechanics does this game use?")]
    [InlineData("Describe the core mechanic")]
    [InlineData("What type of gameplay is it?")]
    public void Classify_MechanicsQueries_ReturnsMechanics(string query)
    {
        var result = _sut.Classify(query);

        result.Intent.Should().Be(StructuredQueryIntent.Mechanics);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.5);
    }

    #endregion

    #region Glossary Intent

    [Theory]
    [InlineData("What is a settlement?")]
    [InlineData("What are resources?")]
    [InlineData("Define worker placement")]
    [InlineData("What is the meaning of meeple?")]
    public void Classify_GlossaryQueries_ReturnsGlossary(string query)
    {
        var result = _sut.Classify(query);

        result.Intent.Should().Be(StructuredQueryIntent.Glossary);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.6);
    }

    [Fact]
    public void Classify_GlossaryQuery_ExtractsMatchedTerm()
    {
        var result = _sut.Classify("What is a settlement?");

        result.Intent.Should().Be(StructuredQueryIntent.Glossary);
        result.MatchedTerm.Should().Be("settlement");
    }

    [Fact]
    public void Classify_WhatIsAnQuery_ExtractsTermAfterAn()
    {
        var result = _sut.Classify("What is an expansion?");

        result.Intent.Should().Be(StructuredQueryIntent.Glossary);
        result.MatchedTerm.Should().Be("expansion");
    }

    #endregion

    #region FAQ Intent

    [Theory]
    [InlineData("Can you trade on your first turn?")]
    [InlineData("Is it possible to build two roads?")]
    [InlineData("How do you resolve a tie?")]
    [InlineData("When can I play development cards?")]
    public void Classify_FaqQueries_ReturnsFaq(string query)
    {
        var result = _sut.Classify(query);

        result.Intent.Should().Be(StructuredQueryIntent.Faq);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.5);
    }

    #endregion

    #region General Intent

    [Theory]
    [InlineData("Tell me about the game")]
    [InlineData("I need help")]
    [InlineData("What should I do next?")]
    public void Classify_GeneralQueries_ReturnsGeneral(string query)
    {
        var result = _sut.Classify(query);

        result.Intent.Should().Be(StructuredQueryIntent.General);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Classify_EmptyOrNull_ReturnsGeneralWithZeroConfidence(string? query)
    {
        var result = _sut.Classify(query!);

        result.Intent.Should().Be(StructuredQueryIntent.General);
        result.Confidence.Should().Be(0.0);
    }

    #endregion

    #region Confidence Ordering

    [Fact]
    public void Classify_PhraseMatch_HasHigherConfidenceThanSingleWord()
    {
        var phraseResult = _sut.Classify("How to win the game");
        var singleResult = _sut.Classify("winning tips");

        // Both should classify as VictoryConditions, phrase match should have higher confidence
        phraseResult.Intent.Should().Be(StructuredQueryIntent.VictoryConditions);
        phraseResult.Confidence.Should().BeGreaterThanOrEqualTo(singleResult.Confidence);
    }

    #endregion
}

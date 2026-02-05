using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Enums;

/// <summary>
/// Unit tests for RagStrategy enum and extensions.
/// Issue #3436: Part of tier-strategy-model architecture.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagStrategyTests
{
    #region GetDisplayName Tests

    [Theory]
    [InlineData(RagStrategy.Fast, "FAST")]
    [InlineData(RagStrategy.Balanced, "BALANCED")]
    [InlineData(RagStrategy.Precise, "PRECISE")]
    [InlineData(RagStrategy.Expert, "EXPERT")]
    [InlineData(RagStrategy.Consensus, "CONSENSUS")]
    [InlineData(RagStrategy.SentenceWindow, "SENTENCE_WINDOW")]
    [InlineData(RagStrategy.Iterative, "ITERATIVE")]
    [InlineData(RagStrategy.Custom, "CUSTOM")]
    [InlineData(RagStrategy.MultiAgent, "MULTI_AGENT")]
    [InlineData(RagStrategy.StepBack, "STEP_BACK")]
    [InlineData(RagStrategy.QueryExpansion, "QUERY_EXPANSION")]
    public void GetDisplayName_AllStrategies_ReturnsUpperCaseName(RagStrategy strategy, string expectedName)
    {
        // Act
        var displayName = strategy.GetDisplayName();

        // Assert
        Assert.Equal(expectedName, displayName);
    }

    #endregion

    #region Parse Tests

    [Theory]
    [InlineData("FAST", RagStrategy.Fast)]
    [InlineData("fast", RagStrategy.Fast)]
    [InlineData("Fast", RagStrategy.Fast)]
    [InlineData("BALANCED", RagStrategy.Balanced)]
    [InlineData("balanced", RagStrategy.Balanced)]
    [InlineData("PRECISE", RagStrategy.Precise)]
    [InlineData("precise", RagStrategy.Precise)]
    [InlineData("EXPERT", RagStrategy.Expert)]
    [InlineData("CONSENSUS", RagStrategy.Consensus)]
    [InlineData("SentenceWindow", RagStrategy.SentenceWindow)]
    [InlineData("sentencewindow", RagStrategy.SentenceWindow)]
    [InlineData("Iterative", RagStrategy.Iterative)]
    [InlineData("iterative", RagStrategy.Iterative)]
    [InlineData("CUSTOM", RagStrategy.Custom)]
    [InlineData("QueryExpansion", RagStrategy.QueryExpansion)]
    [InlineData("queryexpansion", RagStrategy.QueryExpansion)]
    public void Parse_ValidStrings_ReturnsCorrectStrategy(string input, RagStrategy expected)
    {
        // Act
        var result = RagStrategyExtensions.Parse(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("INVALID")]
    [InlineData("UNKNOWN")]
    [InlineData("SPEEDY")]
    public void Parse_InvalidStrings_ThrowsArgumentException(string input)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => RagStrategyExtensions.Parse(input));
        Assert.Contains("Invalid RAG strategy", exception.Message);
    }

    #endregion

    #region TryParse Tests

    [Theory]
    [InlineData("FAST", true, RagStrategy.Fast)]
    [InlineData("fast", true, RagStrategy.Fast)]
    [InlineData("BALANCED", true, RagStrategy.Balanced)]
    [InlineData("balanced", true, RagStrategy.Balanced)]
    [InlineData("PRECISE", true, RagStrategy.Precise)]
    [InlineData("precise", true, RagStrategy.Precise)]
    [InlineData("EXPERT", true, RagStrategy.Expert)]
    [InlineData("CONSENSUS", true, RagStrategy.Consensus)]
    [InlineData("SentenceWindow", true, RagStrategy.SentenceWindow)]
    [InlineData("sentencewindow", true, RagStrategy.SentenceWindow)]
    [InlineData("Iterative", true, RagStrategy.Iterative)]
    [InlineData("iterative", true, RagStrategy.Iterative)]
    [InlineData("CUSTOM", true, RagStrategy.Custom)]
    [InlineData("MultiAgent", true, RagStrategy.MultiAgent)]
    [InlineData("multiagent", true, RagStrategy.MultiAgent)]
    [InlineData("StepBack", true, RagStrategy.StepBack)]
    [InlineData("stepback", true, RagStrategy.StepBack)]
    [InlineData("QueryExpansion", true, RagStrategy.QueryExpansion)]
    [InlineData("queryexpansion", true, RagStrategy.QueryExpansion)]
    [InlineData("INVALID", false, RagStrategy.Fast)]
    [InlineData("", false, RagStrategy.Fast)]
    [InlineData(null, false, RagStrategy.Fast)]
    public void TryParse_VariousInputs_ReturnsExpectedResult(
        string? input, bool expectedSuccess, RagStrategy expectedStrategy)
    {
        // Act
        var success = RagStrategyExtensions.TryParse(input, out var result);

        // Assert
        Assert.Equal(expectedSuccess, success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedStrategy, result);
        }
    }

    [Fact]
    public void TryParse_WhitespaceInput_ReturnsFalse()
    {
        // Act
        var success = RagStrategyExtensions.TryParse("   ", out _);

        // Assert
        Assert.False(success);
    }

    [Fact]
    public void TryParse_TrimmedInput_Works()
    {
        // Act
        var success = RagStrategyExtensions.TryParse("  FAST  ", out var result);

        // Assert
        Assert.True(success);
        Assert.Equal(RagStrategy.Fast, result);
    }

    #endregion

    #region GetComplexityLevel Tests

    [Theory]
    [InlineData(RagStrategy.Fast, 0)]
    [InlineData(RagStrategy.Balanced, 1)]
    [InlineData(RagStrategy.Precise, 2)]
    [InlineData(RagStrategy.Expert, 3)]
    [InlineData(RagStrategy.Consensus, 4)]
    [InlineData(RagStrategy.SentenceWindow, 5)]
    [InlineData(RagStrategy.Iterative, 6)]
    [InlineData(RagStrategy.Custom, 7)]
    [InlineData(RagStrategy.MultiAgent, 8)]
    [InlineData(RagStrategy.StepBack, 9)]
    [InlineData(RagStrategy.QueryExpansion, 10)]
    public void GetComplexityLevel_AllStrategies_ReturnsCorrectLevel(RagStrategy strategy, int expectedLevel)
    {
        // Act
        var level = strategy.GetComplexityLevel();

        // Assert
        Assert.Equal(expectedLevel, level);
    }

    [Fact]
    public void GetComplexityLevel_StrategiesOrderedByComplexity()
    {
        // Arrange
        var strategies = Enum.GetValues<RagStrategy>().OrderBy(s => s.GetComplexityLevel()).ToList();

        // Assert
        Assert.Equal(RagStrategy.Fast, strategies[0]);
        Assert.Equal(RagStrategy.Balanced, strategies[1]);
        Assert.Equal(RagStrategy.Precise, strategies[2]);
        Assert.Equal(RagStrategy.Expert, strategies[3]);
        Assert.Equal(RagStrategy.Consensus, strategies[4]);
        Assert.Equal(RagStrategy.SentenceWindow, strategies[5]);
        Assert.Equal(RagStrategy.Iterative, strategies[6]);
        Assert.Equal(RagStrategy.Custom, strategies[7]);
        Assert.Equal(RagStrategy.MultiAgent, strategies[8]);
        Assert.Equal(RagStrategy.StepBack, strategies[9]);
        Assert.Equal(RagStrategy.QueryExpansion, strategies[10]);
    }

    #endregion

    #region RequiresAdmin Tests

    [Theory]
    [InlineData(RagStrategy.Fast, false)]
    [InlineData(RagStrategy.Balanced, false)]
    [InlineData(RagStrategy.Precise, false)]
    [InlineData(RagStrategy.Expert, false)]
    [InlineData(RagStrategy.Consensus, false)]
    [InlineData(RagStrategy.SentenceWindow, false)]
    [InlineData(RagStrategy.Iterative, false)]
    [InlineData(RagStrategy.Custom, true)]
    [InlineData(RagStrategy.MultiAgent, false)]
    [InlineData(RagStrategy.StepBack, false)]
    [InlineData(RagStrategy.QueryExpansion, false)]
    public void RequiresAdmin_AllStrategies_OnlyCustomRequiresAdmin(RagStrategy strategy, bool expectedRequiresAdmin)
    {
        // Act
        var requiresAdmin = strategy.RequiresAdmin();

        // Assert
        Assert.Equal(expectedRequiresAdmin, requiresAdmin);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void RagStrategy_HasExpectedNumberOfValues()
    {
        // Assert
        var values = Enum.GetValues<RagStrategy>();
        Assert.Equal(11, values.Length);
    }

    [Fact]
    public void RagStrategy_AllValuesHaveUniqueComplexityLevels()
    {
        // Arrange
        var strategies = Enum.GetValues<RagStrategy>();
        var levels = strategies.Select(s => s.GetComplexityLevel()).ToList();

        // Assert
        Assert.Equal(levels.Count, levels.Distinct().Count());
    }

    #endregion
}

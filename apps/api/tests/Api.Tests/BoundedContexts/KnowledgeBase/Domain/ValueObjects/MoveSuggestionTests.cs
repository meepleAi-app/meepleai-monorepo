using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Tests for the MoveSuggestion record and RiskLevel enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 9
/// </summary>
[Trait("Category", "Unit")]
public sealed class MoveSuggestionTests
{
    #region RiskLevel Enum Tests

    [Fact]
    public void RiskLevel_Low_HasCorrectValue()
    {
        ((int)RiskLevel.Low).Should().Be(0);
    }

    [Fact]
    public void RiskLevel_Medium_HasCorrectValue()
    {
        ((int)RiskLevel.Medium).Should().Be(1);
    }

    [Fact]
    public void RiskLevel_High_HasCorrectValue()
    {
        ((int)RiskLevel.High).Should().Be(2);
    }

    [Fact]
    public void RiskLevel_HasThreeValues()
    {
        var values = Enum.GetValues<RiskLevel>();
        values.Should().HaveCount(3);
    }

    #endregion

    #region Create Factory Tests

    [Fact]
    public void Create_WithValidData_ReturnsSuggestion()
    {
        // Act
        var suggestion = MoveSuggestion.Create(
            action: "Build a settlement",
            reasoning: "Good resource placement",
            risk: RiskLevel.Low,
            confidenceScore: 0.85f);

        // Assert
        suggestion.Action.Should().Be("Build a settlement");
        suggestion.Reasoning.Should().Be("Good resource placement");
        suggestion.Risk.Should().Be(RiskLevel.Low);
        suggestion.ConfidenceScore.Should().Be(0.85f);
        suggestion.StateChange.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithStateChange_IncludesStateChange()
    {
        // Arrange
        var stateChange = new Dictionary<string, object>
        {
            { "settlements", 1 },
            { "resources.wood", -4 },
            { "resources.brick", -4 }
        };

        // Act
        var suggestion = MoveSuggestion.Create(
            action: "Build a settlement",
            reasoning: "Expand territory",
            risk: RiskLevel.Medium,
            confidenceScore: 0.75f,
            stateChange: stateChange);

        // Assert
        suggestion.StateChange.Should().HaveCount(3);
        suggestion.StateChange["settlements"].Should().Be(1);
        suggestion.StateChange["resources.wood"].Should().Be(-4);
    }

    [Fact]
    public void Create_WithNullStateChange_UsesEmptyDictionary()
    {
        // Act
        var suggestion = MoveSuggestion.Create(
            action: "Trade",
            reasoning: "Need resources",
            risk: RiskLevel.Low,
            confidenceScore: 0.9f,
            stateChange: null);

        // Assert
        suggestion.StateChange.Should().NotBeNull();
        suggestion.StateChange.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithMinConfidenceScore_Succeeds()
    {
        // Act
        var suggestion = MoveSuggestion.Create(
            action: "Action",
            reasoning: "Reasoning",
            risk: RiskLevel.High,
            confidenceScore: 0.0f);

        // Assert
        suggestion.ConfidenceScore.Should().Be(0.0f);
    }

    [Fact]
    public void Create_WithMaxConfidenceScore_Succeeds()
    {
        // Act
        var suggestion = MoveSuggestion.Create(
            action: "Action",
            reasoning: "Reasoning",
            risk: RiskLevel.Low,
            confidenceScore: 1.0f);

        // Assert
        suggestion.ConfidenceScore.Should().Be(1.0f);
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyAction_ThrowsArgumentException(string? action)
    {
        // Act
        var act = () => MoveSuggestion.Create(
            action: action!,
            reasoning: "Valid reasoning",
            risk: RiskLevel.Low,
            confidenceScore: 0.5f);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Action cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyReasoning_ThrowsArgumentException(string? reasoning)
    {
        // Act
        var act = () => MoveSuggestion.Create(
            action: "Valid action",
            reasoning: reasoning!,
            risk: RiskLevel.Low,
            confidenceScore: 0.5f);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Reasoning cannot be empty*");
    }

    [Fact]
    public void Create_WithConfidenceScoreBelowZero_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => MoveSuggestion.Create(
            action: "Action",
            reasoning: "Reasoning",
            risk: RiskLevel.Low,
            confidenceScore: -0.1f);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Confidence score must be between 0.0 and 1.0*");
    }

    [Fact]
    public void Create_WithConfidenceScoreAboveOne_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => MoveSuggestion.Create(
            action: "Action",
            reasoning: "Reasoning",
            risk: RiskLevel.Low,
            confidenceScore: 1.1f);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Confidence score must be between 0.0 and 1.0*");
    }

    #endregion

    #region Record Property Tests

    [Fact]
    public void TwoSuggestions_WithSameValues_HaveSameActionAndReasoning()
    {
        // Arrange
        var suggestion1 = MoveSuggestion.Create("Action", "Reasoning", RiskLevel.Low, 0.5f);
        var suggestion2 = MoveSuggestion.Create("Action", "Reasoning", RiskLevel.Low, 0.5f);

        // Assert - Compare individual properties since records with dictionary references aren't equal
        suggestion1.Action.Should().Be(suggestion2.Action);
        suggestion1.Reasoning.Should().Be(suggestion2.Reasoning);
        suggestion1.Risk.Should().Be(suggestion2.Risk);
        suggestion1.ConfidenceScore.Should().Be(suggestion2.ConfidenceScore);
    }

    [Fact]
    public void TwoSuggestions_WithDifferentActions_HaveDifferentValues()
    {
        // Arrange
        var suggestion1 = MoveSuggestion.Create("Action A", "Reasoning", RiskLevel.Low, 0.5f);
        var suggestion2 = MoveSuggestion.Create("Action B", "Reasoning", RiskLevel.Low, 0.5f);

        // Assert
        suggestion1.Action.Should().NotBe(suggestion2.Action);
    }

    [Fact]
    public void TwoSuggestions_WithDifferentRisk_HaveDifferentRiskLevels()
    {
        // Arrange
        var suggestion1 = MoveSuggestion.Create("Action", "Reasoning", RiskLevel.Low, 0.5f);
        var suggestion2 = MoveSuggestion.Create("Action", "Reasoning", RiskLevel.High, 0.5f);

        // Assert
        suggestion1.Risk.Should().NotBe(suggestion2.Risk);
    }

    [Fact]
    public void TwoSuggestions_WithDifferentConfidence_HaveDifferentScores()
    {
        // Arrange
        var suggestion1 = MoveSuggestion.Create("Action", "Reasoning", RiskLevel.Low, 0.5f);
        var suggestion2 = MoveSuggestion.Create("Action", "Reasoning", RiskLevel.Low, 0.8f);

        // Assert
        suggestion1.ConfidenceScore.Should().NotBe(suggestion2.ConfidenceScore);
    }

    #endregion

    #region StateChange Immutability Tests

    [Fact]
    public void StateChange_IsReadOnly()
    {
        // Arrange
        var stateChange = new Dictionary<string, object> { { "key", "value" } };
        var suggestion = MoveSuggestion.Create(
            action: "Action",
            reasoning: "Reasoning",
            risk: RiskLevel.Low,
            confidenceScore: 0.5f,
            stateChange: stateChange);

        // Assert - Should be read-only
        suggestion.StateChange.Should().BeAssignableTo<IReadOnlyDictionary<string, object>>();
    }

    #endregion
}

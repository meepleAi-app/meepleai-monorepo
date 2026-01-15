using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Unit tests for StateExtractionResult value object.
/// Issue #2468 - Ledger Mode Test Suite
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class StateExtractionResultTests
{
    #region Create Method Tests

    [Fact]
    public void Create_WithValidInputs_CreatesInstance()
    {
        // Arrange
        var extractedState = new Dictionary<string, object> { { "score", 5 } };

        // Act
        var result = StateExtractionResult.Create(
            changeType: StateChangeType.ScoreChange,
            originalMessage: "ho 5 punti",
            confidence: 0.9f,
            extractedState: extractedState);

        // Assert
        result.ChangeType.Should().Be(StateChangeType.ScoreChange);
        result.OriginalMessage.Should().Be("ho 5 punti");
        result.Confidence.Should().Be(0.9f);
        result.ExtractedState.Should().ContainKey("score");
        result.ExtractedState["score"].Should().Be(5);
    }

    [Fact]
    public void Create_WithPlayerName_TrimsAndStoresPlayerName()
    {
        // Act
        var result = StateExtractionResult.Create(
            changeType: StateChangeType.ScoreChange,
            originalMessage: "Marco ha 5 punti",
            confidence: 0.9f,
            playerName: "  Marco  ");

        // Assert
        result.PlayerName.Should().Be("Marco");
    }

    [Fact]
    public void Create_WithWarnings_StoresWarningsList()
    {
        // Arrange
        var warnings = new List<string> { "Warning 1", "Warning 2" };

        // Act
        var result = StateExtractionResult.Create(
            changeType: StateChangeType.Composite,
            originalMessage: "complex message",
            confidence: 0.8f,
            warnings: warnings);

        // Assert
        result.Warnings.Should().HaveCount(2);
        result.Warnings.Should().Contain("Warning 1");
        result.Warnings.Should().Contain("Warning 2");
    }

    [Fact]
    public void Create_WithRequiresConfirmationTrue_SetsFlag()
    {
        // Act
        var result = StateExtractionResult.Create(
            changeType: StateChangeType.ScoreChange,
            originalMessage: "test",
            confidence: 0.5f,
            requiresConfirmation: true);

        // Assert
        result.RequiresConfirmation.Should().BeTrue();
    }

    [Fact]
    public void Create_WithRequiresConfirmationFalse_ClearsFlag()
    {
        // Act
        var result = StateExtractionResult.Create(
            changeType: StateChangeType.ScoreChange,
            originalMessage: "test",
            confidence: 0.95f,
            requiresConfirmation: false);

        // Assert
        result.RequiresConfirmation.Should().BeFalse();
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Create_WithEmptyMessage_ThrowsArgumentException()
    {
        // Act
        var act = () => StateExtractionResult.Create(
            changeType: StateChangeType.ScoreChange,
            originalMessage: "",
            confidence: 0.9f);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Original message cannot be empty*");
    }

    [Fact]
    public void Create_WithWhitespaceMessage_ThrowsArgumentException()
    {
        // Act
        var act = () => StateExtractionResult.Create(
            changeType: StateChangeType.ScoreChange,
            originalMessage: "   ",
            confidence: 0.9f);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Original message cannot be empty*");
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    [InlineData(-1.0f)]
    [InlineData(2.0f)]
    public void Create_WithInvalidConfidence_ThrowsArgumentOutOfRangeException(float invalidConfidence)
    {
        // Act
        var act = () => StateExtractionResult.Create(
            changeType: StateChangeType.ScoreChange,
            originalMessage: "test",
            confidence: invalidConfidence);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Confidence must be between 0.0 and 1.0*");
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    public void Create_WithValidConfidence_Succeeds(float validConfidence)
    {
        // Act
        var result = StateExtractionResult.Create(
            changeType: StateChangeType.ScoreChange,
            originalMessage: "test",
            confidence: validConfidence);

        // Assert
        result.Confidence.Should().Be(validConfidence);
    }

    #endregion

    #region NoChange Factory Tests

    [Fact]
    public void NoChange_CreatesNoChangeResult()
    {
        // Act
        var result = StateExtractionResult.NoChange("no state change");

        // Assert
        result.ChangeType.Should().Be(StateChangeType.NoChange);
        result.OriginalMessage.Should().Be("no state change");
        result.Confidence.Should().Be(1.0f);
        result.RequiresConfirmation.Should().BeFalse();
        result.HasStateChanges.Should().BeFalse();
        result.ExtractedState.Should().BeEmpty();
    }

    #endregion

    #region HasStateChanges Property Tests

    [Fact]
    public void HasStateChanges_WithNoChangeType_ReturnsFalse()
    {
        // Arrange
        var result = StateExtractionResult.NoChange("test");

        // Assert
        result.HasStateChanges.Should().BeFalse();
    }

    [Fact]
    public void HasStateChanges_WithScoreChangeAndExtractedState_ReturnsTrue()
    {
        // Arrange
        var result = StateExtractionResult.Create(
            changeType: StateChangeType.ScoreChange,
            originalMessage: "test",
            confidence: 0.9f,
            extractedState: new Dictionary<string, object> { { "score", 5 } });

        // Assert
        result.HasStateChanges.Should().BeTrue();
    }

    [Fact]
    public void HasStateChanges_WithChangeTypeButEmptyState_ReturnsFalse()
    {
        // Arrange
        var result = StateExtractionResult.Create(
            changeType: StateChangeType.ScoreChange,
            originalMessage: "test",
            confidence: 0.9f,
            extractedState: new Dictionary<string, object>());

        // Assert
        result.HasStateChanges.Should().BeFalse();
    }

    [Fact]
    public void HasStateChanges_WithNullExtractedState_ReturnsFalse()
    {
        // Arrange
        var result = StateExtractionResult.Create(
            changeType: StateChangeType.ScoreChange,
            originalMessage: "test",
            confidence: 0.9f,
            extractedState: null);

        // Assert
        result.HasStateChanges.Should().BeFalse();
    }

    #endregion

    #region StateChangeType Enum Tests

    [Fact]
    public void StateChangeType_NoChange_HasValue0()
    {
        StateChangeType.NoChange.Should().Be((StateChangeType)0);
    }

    [Fact]
    public void StateChangeType_ScoreChange_HasValue1()
    {
        StateChangeType.ScoreChange.Should().Be((StateChangeType)1);
    }

    [Fact]
    public void StateChangeType_ResourceChange_HasValue2()
    {
        StateChangeType.ResourceChange.Should().Be((StateChangeType)2);
    }

    [Fact]
    public void StateChangeType_PlayerAction_HasValue3()
    {
        StateChangeType.PlayerAction.Should().Be((StateChangeType)3);
    }

    [Fact]
    public void StateChangeType_TurnChange_HasValue4()
    {
        StateChangeType.TurnChange.Should().Be((StateChangeType)4);
    }

    [Fact]
    public void StateChangeType_PhaseChange_HasValue5()
    {
        StateChangeType.PhaseChange.Should().Be((StateChangeType)5);
    }

    [Fact]
    public void StateChangeType_Composite_HasValue6()
    {
        StateChangeType.Composite.Should().Be((StateChangeType)6);
    }

    #endregion

    #region Immutability Tests

    [Fact]
    public void ExtractedState_IsReadOnly()
    {
        // Arrange
        var extractedState = new Dictionary<string, object> { { "score", 5 } };
        var result = StateExtractionResult.Create(
            changeType: StateChangeType.ScoreChange,
            originalMessage: "test",
            confidence: 0.9f,
            extractedState: extractedState);

        // Act
        var isReadOnly = result.ExtractedState is IReadOnlyDictionary<string, object>;

        // Assert
        isReadOnly.Should().BeTrue();
    }

    [Fact]
    public void Warnings_IsReadOnly()
    {
        // Arrange
        var warnings = new List<string> { "Warning" };
        var result = StateExtractionResult.Create(
            changeType: StateChangeType.ScoreChange,
            originalMessage: "test",
            confidence: 0.9f,
            warnings: warnings);

        // Act
        var isReadOnly = result.Warnings is IReadOnlyList<string>;

        // Assert
        isReadOnly.Should().BeTrue();
    }

    #endregion
}

using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Tests for the StateChange value object and StateChangeType enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 3
/// </summary>
[Trait("Category", "Unit")]
public sealed class StateChangeTests
{
    #region StateChangeType Enum Tests

    [Fact]
    public void StateChangeType_Score_HasCorrectValue()
    {
        ((int)StateChangeType.Score).Should().Be(0);
    }

    [Fact]
    public void StateChangeType_Resource_HasCorrectValue()
    {
        ((int)StateChangeType.Resource).Should().Be(1);
    }

    [Fact]
    public void StateChangeType_Turn_HasCorrectValue()
    {
        ((int)StateChangeType.Turn).Should().Be(2);
    }

    [Fact]
    public void StateChangeType_Phase_HasCorrectValue()
    {
        ((int)StateChangeType.Phase).Should().Be(3);
    }

    [Fact]
    public void StateChangeType_Custom_HasCorrectValue()
    {
        ((int)StateChangeType.Custom).Should().Be(4);
    }

    [Fact]
    public void StateChangeType_HasFiveValues()
    {
        var values = Enum.GetValues<StateChangeType>();
        values.Should().HaveCount(5);
    }

    #endregion

    #region Create Factory Tests

    [Fact]
    public void Create_WithMinimalParameters_ReturnsInstance()
    {
        // Act
        var change = StateChange.Create(
            StateChangeType.Score,
            "score",
            "10");

        // Assert
        change.ChangeType.Should().Be(StateChangeType.Score);
        change.FieldName.Should().Be("score");
        change.NewValue.Should().Be("10");
        change.OldValue.Should().BeNull();
        change.PlayerName.Should().BeNull();
        change.Source.Should().Be("ledger-agent");
        change.IsConfirmed.Should().BeFalse();
        change.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithAllParameters_SetsAllProperties()
    {
        // Act
        var change = StateChange.Create(
            StateChangeType.Resource,
            "wood",
            "15",
            oldValue: "10",
            playerName: "Alice",
            source: "manual",
            isConfirmed: true);

        // Assert
        change.ChangeType.Should().Be(StateChangeType.Resource);
        change.FieldName.Should().Be("wood");
        change.NewValue.Should().Be("15");
        change.OldValue.Should().Be("10");
        change.PlayerName.Should().Be("Alice");
        change.Source.Should().Be("manual");
        change.IsConfirmed.Should().BeTrue();
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        // Act
        var change = StateChange.Create(
            StateChangeType.Score,
            "  fieldName  ",
            "  newValue  ",
            oldValue: "  oldValue  ",
            playerName: "  Player  ",
            source: "  api  ");

        // Assert
        change.FieldName.Should().Be("fieldName");
        change.NewValue.Should().Be("newValue");
        change.OldValue.Should().Be("oldValue");
        change.PlayerName.Should().Be("Player");
        change.Source.Should().Be("api");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyFieldName_ThrowsArgumentException(string? fieldName)
    {
        // Act
        var action = () => StateChange.Create(
            StateChangeType.Score,
            fieldName!,
            "10");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("Field name cannot be empty*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyNewValue_ThrowsArgumentException(string? newValue)
    {
        // Act
        var action = () => StateChange.Create(
            StateChangeType.Score,
            "field",
            newValue!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("New value cannot be empty*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptySource_ThrowsArgumentException(string source)
    {
        // Act
        var action = () => StateChange.Create(
            StateChangeType.Score,
            "field",
            "value",
            source: source);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("Source cannot be empty*");
    }

    #endregion

    #region Confirm Tests

    [Fact]
    public void Confirm_SetsIsConfirmedToTrue()
    {
        // Arrange
        var change = StateChange.Create(
            StateChangeType.Score,
            "score",
            "10",
            isConfirmed: false);

        // Act
        var confirmed = change.Confirm();

        // Assert
        confirmed.IsConfirmed.Should().BeTrue();
    }

    [Fact]
    public void Confirm_ReturnsNewInstance()
    {
        // Arrange
        var original = StateChange.Create(
            StateChangeType.Score,
            "score",
            "10");

        // Act
        var confirmed = original.Confirm();

        // Assert
        original.IsConfirmed.Should().BeFalse();
        confirmed.IsConfirmed.Should().BeTrue();
        ReferenceEquals(original, confirmed).Should().BeFalse();
    }

    [Fact]
    public void Confirm_PreservesOtherProperties()
    {
        // Arrange
        var original = StateChange.Create(
            StateChangeType.Resource,
            "gold",
            "100",
            oldValue: "50",
            playerName: "Bob",
            source: "api");

        // Act
        var confirmed = original.Confirm();

        // Assert
        confirmed.ChangeType.Should().Be(StateChangeType.Resource);
        confirmed.FieldName.Should().Be("gold");
        confirmed.NewValue.Should().Be("100");
        confirmed.OldValue.Should().Be("50");
        confirmed.PlayerName.Should().Be("Bob");
        confirmed.Source.Should().Be("api");
        confirmed.Timestamp.Should().Be(original.Timestamp);
    }

    #endregion

    #region ChangeType Usage Tests

    [Fact]
    public void Create_WithScoreChangeType_CreatesScoreChange()
    {
        // Act
        var change = StateChange.Create(
            StateChangeType.Score,
            "victoryPoints",
            "25",
            oldValue: "20",
            playerName: "Alice");

        // Assert
        change.ChangeType.Should().Be(StateChangeType.Score);
    }

    [Fact]
    public void Create_WithResourceChangeType_CreatesResourceChange()
    {
        // Act
        var change = StateChange.Create(
            StateChangeType.Resource,
            "wood",
            "8",
            oldValue: "5",
            playerName: "Bob");

        // Assert
        change.ChangeType.Should().Be(StateChangeType.Resource);
    }

    [Fact]
    public void Create_WithTurnChangeType_CreatesTurnChange()
    {
        // Act
        var change = StateChange.Create(
            StateChangeType.Turn,
            "currentPlayer",
            "2",
            oldValue: "1");

        // Assert
        change.ChangeType.Should().Be(StateChangeType.Turn);
    }

    [Fact]
    public void Create_WithPhaseChangeType_CreatesPhaseChange()
    {
        // Act
        var change = StateChange.Create(
            StateChangeType.Phase,
            "gamePhase",
            "Scoring",
            oldValue: "InProgress");

        // Assert
        change.ChangeType.Should().Be(StateChangeType.Phase);
    }

    [Fact]
    public void Create_WithCustomChangeType_CreatesCustomChange()
    {
        // Act
        var change = StateChange.Create(
            StateChangeType.Custom,
            "specialAbility",
            "activated",
            playerName: "Charlie");

        // Assert
        change.ChangeType.Should().Be(StateChangeType.Custom);
    }

    #endregion

    #region Timestamp Tests

    [Fact]
    public void Create_SetsTimestampToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var change = StateChange.Create(
            StateChangeType.Score,
            "score",
            "10");

        var after = DateTime.UtcNow;

        // Assert
        change.Timestamp.Should().BeOnOrAfter(before);
        change.Timestamp.Should().BeOnOrBefore(after);
    }

    #endregion

    #region Source Variations Tests

    [Theory]
    [InlineData("ledger-agent")]
    [InlineData("manual")]
    [InlineData("api")]
    [InlineData("import")]
    public void Create_WithValidSource_SetsSource(string source)
    {
        // Act
        var change = StateChange.Create(
            StateChangeType.Score,
            "score",
            "10",
            source: source);

        // Assert
        change.Source.Should().Be(source);
    }

    #endregion
}

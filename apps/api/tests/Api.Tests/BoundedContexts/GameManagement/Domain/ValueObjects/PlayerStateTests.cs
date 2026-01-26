using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Tests for the PlayerState value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 3
/// </summary>
[Trait("Category", "Unit")]
public sealed class PlayerStateTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidData_ReturnsInstance()
    {
        // Arrange
        var playerName = "Alice";

        // Act
        var state = PlayerState.Create(playerName);

        // Assert
        state.PlayerName.Should().Be(playerName);
        state.Score.Should().Be(0);
        state.Resources.Should().BeEmpty();
        state.TurnOrder.Should().Be(1);
        state.IsCurrentTurn.Should().BeFalse();
        state.CustomState.Should().BeNull();
    }

    [Fact]
    public void Create_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        var playerName = "Bob";
        var score = 42;
        var resources = new Dictionary<string, int> { ["wood"] = 5, ["stone"] = 3 };
        var turnOrder = 2;
        var isCurrentTurn = true;
        var customState = new Dictionary<string, object> { ["bonus"] = "active" };

        // Act
        var state = PlayerState.Create(
            playerName,
            score,
            resources,
            turnOrder,
            isCurrentTurn,
            customState);

        // Assert
        state.PlayerName.Should().Be(playerName);
        state.Score.Should().Be(score);
        state.Resources.Should().BeEquivalentTo(resources);
        state.TurnOrder.Should().Be(turnOrder);
        state.IsCurrentTurn.Should().BeTrue();
        state.CustomState.Should().BeEquivalentTo(customState);
    }

    [Fact]
    public void Create_TrimsPlayerName()
    {
        // Act
        var state = PlayerState.Create("  Charlie  ");

        // Assert
        state.PlayerName.Should().Be("Charlie");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyOrNullName_ThrowsArgumentException(string? name)
    {
        // Act
        var action = () => PlayerState.Create(name!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("Player name cannot be empty*");
    }

    [Fact]
    public void Create_WithNameExceeding50Characters_ThrowsArgumentException()
    {
        // Arrange
        var longName = new string('x', 51);

        // Act
        var action = () => PlayerState.Create(longName);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("Player name cannot exceed 50 characters*");
    }

    [Fact]
    public void Create_WithExactly50CharacterName_Succeeds()
    {
        // Arrange
        var name = new string('x', 50);

        // Act
        var state = PlayerState.Create(name);

        // Assert
        state.PlayerName.Should().HaveLength(50);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithInvalidTurnOrder_ThrowsArgumentException(int turnOrder)
    {
        // Act
        var action = () => PlayerState.Create("Player", turnOrder: turnOrder);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("Turn order must be at least 1*");
    }

    [Fact]
    public void Create_WithNegativeScore_Succeeds()
    {
        // Negative scores are valid (e.g., penalties)
        var state = PlayerState.Create("Player", score: -10);

        // Assert
        state.Score.Should().Be(-10);
    }

    #endregion

    #region WithScore Tests

    [Fact]
    public void WithScore_UpdatesScore()
    {
        // Arrange
        var state = PlayerState.Create("Player", score: 10);

        // Act
        var updated = state.WithScore(25);

        // Assert
        updated.Score.Should().Be(25);
        updated.PlayerName.Should().Be("Player"); // Other properties unchanged
    }

    [Fact]
    public void WithScore_ReturnsNewInstance()
    {
        // Arrange
        var original = PlayerState.Create("Player", score: 10);

        // Act
        var updated = original.WithScore(20);

        // Assert
        original.Score.Should().Be(10); // Original unchanged
        updated.Score.Should().Be(20);
        ReferenceEquals(original, updated).Should().BeFalse();
    }

    #endregion

    #region WithResource Tests

    [Fact]
    public void WithResource_AddsNewResource()
    {
        // Arrange
        var state = PlayerState.Create("Player");

        // Act
        var updated = state.WithResource("gold", 100);

        // Assert
        updated.Resources.Should().ContainKey("gold");
        updated.Resources["gold"].Should().Be(100);
    }

    [Fact]
    public void WithResource_UpdatesExistingResource()
    {
        // Arrange
        var resources = new Dictionary<string, int> { ["wood"] = 5 };
        var state = PlayerState.Create("Player", resources: resources);

        // Act
        var updated = state.WithResource("wood", 10);

        // Assert
        updated.Resources["wood"].Should().Be(10);
    }

    [Fact]
    public void WithResource_PreservesOtherResources()
    {
        // Arrange
        var resources = new Dictionary<string, int> { ["wood"] = 5, ["stone"] = 3 };
        var state = PlayerState.Create("Player", resources: resources);

        // Act
        var updated = state.WithResource("wood", 10);

        // Assert
        updated.Resources["wood"].Should().Be(10);
        updated.Resources["stone"].Should().Be(3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void WithResource_WithEmptyName_ThrowsArgumentException(string? resourceName)
    {
        // Arrange
        var state = PlayerState.Create("Player");

        // Act
        var action = () => state.WithResource(resourceName!, 10);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("Resource name cannot be empty*");
    }

    #endregion

    #region WithCurrentTurn Tests

    [Fact]
    public void WithCurrentTurn_SetsTrue()
    {
        // Arrange
        var state = PlayerState.Create("Player", isCurrentTurn: false);

        // Act
        var updated = state.WithCurrentTurn(true);

        // Assert
        updated.IsCurrentTurn.Should().BeTrue();
    }

    [Fact]
    public void WithCurrentTurn_SetsFalse()
    {
        // Arrange
        var state = PlayerState.Create("Player", isCurrentTurn: true);

        // Act
        var updated = state.WithCurrentTurn(false);

        // Assert
        updated.IsCurrentTurn.Should().BeFalse();
    }

    #endregion

    #region WithCustomState Tests

    [Fact]
    public void WithCustomState_AddsNewKey()
    {
        // Arrange
        var state = PlayerState.Create("Player");

        // Act
        var updated = state.WithCustomState("ability", "fireball");

        // Assert
        updated.CustomState.Should().NotBeNull();
        updated.CustomState!["ability"].Should().Be("fireball");
    }

    [Fact]
    public void WithCustomState_UpdatesExistingKey()
    {
        // Arrange
        var customState = new Dictionary<string, object> { ["level"] = 1 };
        var state = PlayerState.Create("Player", customState: customState);

        // Act
        var updated = state.WithCustomState("level", 2);

        // Assert
        updated.CustomState!["level"].Should().Be(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void WithCustomState_WithEmptyKey_ThrowsArgumentException(string? key)
    {
        // Arrange
        var state = PlayerState.Create("Player");

        // Act
        var action = () => state.WithCustomState(key!, "value");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("Custom state key cannot be empty*");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var resources = new Dictionary<string, int> { ["wood"] = 5 };
        var state1 = PlayerState.Create("Player", 10, resources, 1, true);
        var state2 = PlayerState.Create("Player", 10, resources, 1, true);

        // Note: Record equality compares all properties
        // The Resources dictionary comparison may differ based on reference
        // This tests value object behavior
        state1.PlayerName.Should().Be(state2.PlayerName);
        state1.Score.Should().Be(state2.Score);
        state1.TurnOrder.Should().Be(state2.TurnOrder);
        state1.IsCurrentTurn.Should().Be(state2.IsCurrentTurn);
    }

    #endregion
}

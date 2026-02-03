using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Tests for the GameStateSnapshot entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 22
/// </summary>
[Trait("Category", "Unit")]
public sealed class GameStateSnapshotTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidParameters_CreatesSnapshot()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sessionStateId = Guid.NewGuid();
        var state = JsonDocument.Parse("{\"turn\": 1, \"players\": []}");

        // Act
        var snapshot = GameStateSnapshot.Create(
            id,
            sessionStateId,
            state,
            5,
            "After player move",
            "player1");

        // Assert
        snapshot.Id.Should().Be(id);
        snapshot.SessionStateId.Should().Be(sessionStateId);
        snapshot.State.Should().NotBeNull();
        snapshot.TurnNumber.Should().Be(5);
        snapshot.Description.Should().Be("After player move");
        snapshot.CreatedBy.Should().Be("player1");
        snapshot.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithZeroTurnNumber_Succeeds()
    {
        // Arrange
        var state = JsonDocument.Parse("{}");

        // Act
        var snapshot = GameStateSnapshot.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            state,
            0,
            "Initial state",
            "system");

        // Assert
        snapshot.TurnNumber.Should().Be(0);
    }

    [Fact]
    public void Create_TrimsDescription_Succeeds()
    {
        // Arrange
        var state = JsonDocument.Parse("{}");

        // Act
        var snapshot = GameStateSnapshot.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            state,
            1,
            "  After move  ",
            "player1");

        // Assert
        snapshot.Description.Should().Be("After move");
    }

    [Fact]
    public void Create_TrimsCreatedBy_Succeeds()
    {
        // Arrange
        var state = JsonDocument.Parse("{}");

        // Act
        var snapshot = GameStateSnapshot.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            state,
            1,
            "Test",
            "  player1  ");

        // Assert
        snapshot.CreatedBy.Should().Be("player1");
    }

    [Fact]
    public void Create_WithEmptySessionStateId_ThrowsArgumentException()
    {
        // Arrange
        var state = JsonDocument.Parse("{}");

        // Act
        var action = () => GameStateSnapshot.Create(
            Guid.NewGuid(),
            Guid.Empty,
            state,
            1,
            "Test",
            "player1");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("sessionStateId")
            .WithMessage("*SessionStateId cannot be empty*");
    }

    [Fact]
    public void Create_WithNullState_ThrowsArgumentNullException()
    {
        // Act
        var action = () => GameStateSnapshot.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null!,
            1,
            "Test",
            "player1");

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNegativeTurnNumber_ThrowsArgumentException()
    {
        // Arrange
        var state = JsonDocument.Parse("{}");

        // Act
        var action = () => GameStateSnapshot.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            state,
            -1,
            "Test",
            "player1");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("turnNumber")
            .WithMessage("*Turn number cannot be negative*");
    }

    [Fact]
    public void Create_WithEmptyDescription_ThrowsArgumentException()
    {
        // Arrange
        var state = JsonDocument.Parse("{}");

        // Act
        var action = () => GameStateSnapshot.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            state,
            1,
            "",
            "player1");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("description")
            .WithMessage("*Description cannot be empty*");
    }

    [Fact]
    public void Create_WithWhitespaceDescription_ThrowsArgumentException()
    {
        // Arrange
        var state = JsonDocument.Parse("{}");

        // Act
        var action = () => GameStateSnapshot.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            state,
            1,
            "   ",
            "player1");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Description cannot be empty*");
    }

    [Fact]
    public void Create_WithDescriptionOver500Characters_ThrowsArgumentException()
    {
        // Arrange
        var state = JsonDocument.Parse("{}");
        var longDescription = new string('A', 501);

        // Act
        var action = () => GameStateSnapshot.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            state,
            1,
            longDescription,
            "player1");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("description")
            .WithMessage("*Description cannot exceed 500 characters*");
    }

    [Fact]
    public void Create_WithDescriptionExactly500Characters_Succeeds()
    {
        // Arrange
        var state = JsonDocument.Parse("{}");
        var description = new string('A', 500);

        // Act
        var snapshot = GameStateSnapshot.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            state,
            1,
            description,
            "player1");

        // Assert
        snapshot.Description.Should().HaveLength(500);
    }

    [Fact]
    public void Create_WithEmptyCreatedBy_ThrowsArgumentException()
    {
        // Arrange
        var state = JsonDocument.Parse("{}");

        // Act
        var action = () => GameStateSnapshot.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            state,
            1,
            "Test",
            "");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("createdBy")
            .WithMessage("*CreatedBy cannot be empty*");
    }

    [Fact]
    public void Create_WithWhitespaceCreatedBy_ThrowsArgumentException()
    {
        // Arrange
        var state = JsonDocument.Parse("{}");

        // Act
        var action = () => GameStateSnapshot.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            state,
            1,
            "Test",
            "   ");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*CreatedBy cannot be empty*");
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesSnapshot()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sessionStateId = Guid.NewGuid();
        var state = JsonDocument.Parse("{\"data\": \"test\"}");
        var createdAt = DateTime.UtcNow.AddMinutes(-5);

        // Act
        var snapshot = new GameStateSnapshot(
            id,
            sessionStateId,
            state,
            10,
            "Reconstructed snapshot",
            createdAt,
            "admin");

        // Assert
        snapshot.Id.Should().Be(id);
        snapshot.SessionStateId.Should().Be(sessionStateId);
        snapshot.State.Should().NotBeNull();
        snapshot.TurnNumber.Should().Be(10);
        snapshot.Description.Should().Be("Reconstructed snapshot");
        snapshot.CreatedAt.Should().Be(createdAt);
        snapshot.CreatedBy.Should().Be("admin");
    }

    #endregion

    #region GetStateAsString Tests

    [Fact]
    public void GetStateAsString_ReturnsJsonString()
    {
        // Arrange
        var state = JsonDocument.Parse("{\"turn\": 5, \"players\": [\"Alice\", \"Bob\"]}");
        var snapshot = GameStateSnapshot.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            state,
            5,
            "Test snapshot",
            "tester");

        // Act
        var result = snapshot.GetStateAsString();

        // Assert
        result.Should().Contain("\"turn\"");
        result.Should().Contain("5");
        result.Should().Contain("\"players\"");
        result.Should().Contain("Alice");
        result.Should().Contain("Bob");
    }

    [Fact]
    public void GetStateAsString_WithEmptyObject_ReturnsEmptyJson()
    {
        // Arrange
        var state = JsonDocument.Parse("{}");
        var snapshot = GameStateSnapshot.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            state,
            1,
            "Empty state",
            "tester");

        // Act
        var result = snapshot.GetStateAsString();

        // Assert
        result.Should().Be("{}");
    }

    [Fact]
    public void GetStateAsString_WithNestedObject_ReturnsCorrectJson()
    {
        // Arrange
        var state = JsonDocument.Parse("{\"game\": {\"board\": [[1, 2], [3, 4]]}}");
        var snapshot = GameStateSnapshot.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            state,
            1,
            "Nested state",
            "tester");

        // Act
        var result = snapshot.GetStateAsString();

        // Assert
        result.Should().Contain("\"game\"");
        result.Should().Contain("\"board\"");
    }

    #endregion

    #region Complex Workflow Tests

    [Fact]
    public void Create_MultipleSnapshots_HaveUniqueIds()
    {
        // Arrange
        var sessionStateId = Guid.NewGuid();
        var state = JsonDocument.Parse("{}");

        // Act
        var snapshot1 = GameStateSnapshot.Create(
            Guid.NewGuid(),
            sessionStateId,
            state,
            1,
            "Snapshot 1",
            "player1");

        var snapshot2 = GameStateSnapshot.Create(
            Guid.NewGuid(),
            sessionStateId,
            state,
            2,
            "Snapshot 2",
            "player1");

        // Assert
        snapshot1.Id.Should().NotBe(snapshot2.Id);
    }

    [Fact]
    public void Create_WithComplexGameState_PreservesAllData()
    {
        // Arrange
        var complexState = JsonDocument.Parse(@"{
            ""turn"": 15,
            ""phase"": ""action"",
            ""players"": [
                {""name"": ""Alice"", ""score"": 42, ""resources"": {""gold"": 10, ""wood"": 5}},
                {""name"": ""Bob"", ""score"": 38, ""resources"": {""gold"": 8, ""wood"": 7}}
            ],
            ""board"": [[0, 1, 2], [3, 4, 5], [6, 7, 8]]
        }");

        // Act
        var snapshot = GameStateSnapshot.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            complexState,
            15,
            "Mid-game state with complex board",
            "gamemaster");

        // Assert
        var stateString = snapshot.GetStateAsString();
        stateString.Should().Contain("\"turn\":15");
        stateString.Should().Contain("Alice");
        stateString.Should().Contain("Bob");
        stateString.Should().Contain("\"gold\"");
    }

    #endregion
}

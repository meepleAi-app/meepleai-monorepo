using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

[Trait("Category", TestCategories.Unit)]
public class GameSessionStateDomainTests
{
    [Fact]
    public void Create_WithValidParameters_CreatesSuccessfully()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var initialState = JsonDocument.Parse("{\"player\":\"Alice\",\"score\":0}");

        // Act
        var state = GameSessionState.Create(stateId, sessionId, templateId, initialState, "testuser");

        // Assert
        state.Id.Should().Be(stateId);
        state.GameSessionId.Should().Be(sessionId);
        state.TemplateId.Should().Be(templateId);
        state.Version.Should().Be(1);
        state.LastUpdatedBy.Should().Be("testuser");
        state.CurrentState.Should().NotBeNull();
        state.Snapshots.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithEmptyGameSessionId_ThrowsArgumentException()
    {
        // Arrange
        var initialState = JsonDocument.Parse("{}");

        // Act & Assert
        var act = () =>
            GameSessionState.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), initialState, "user");
        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.Message.Should().ContainEquivalentOf("GameSessionId cannot be empty");
    }

    [Fact]
    public void Create_WithEmptyTemplateId_ThrowsArgumentException()
    {
        // Arrange
        var initialState = JsonDocument.Parse("{}");

        // Act & Assert
        var act = () =>
            GameSessionState.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, initialState, "user");
        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.Message.Should().ContainEquivalentOf("TemplateId cannot be empty");
    }

    [Fact]
    public void Create_WithNullInitialState_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            GameSessionState.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null!, "user");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithEmptyCreatedBy_ThrowsArgumentException()
    {
        // Arrange
        var initialState = JsonDocument.Parse("{}");

        // Act & Assert
        var act = () =>
            GameSessionState.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), initialState, "");
        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.Message.Should().ContainEquivalentOf("CreatedBy cannot be empty");
    }

    [Fact]
    public void UpdateState_WithValidState_UpdatesSuccessfully()
    {
        // Arrange
        var state = CreateTestState();
        var newState = JsonDocument.Parse("{\"player\":\"Bob\",\"score\":10}");

        // Act
        state.UpdateState(newState, "updater");

        // Assert
        state.Version.Should().Be(2); // Version incremented
        state.LastUpdatedBy.Should().Be("updater");
        state.CurrentState.Should().NotBeNull();
    }

    [Fact]
    public void CreateSnapshot_WithValidParams_CreatesSuccessfully()
    {
        // Arrange
        var state = CreateTestState();

        // Act
        var snapshot = state.CreateSnapshot(turnNumber: 1, description: "Turn 1 end", createdBy: "user");

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.TurnNumber.Should().Be(1);
        snapshot.Description.Should().Be("Turn 1 end");
        state.Snapshots.Should().ContainSingle();
    }

    [Fact]
    public void CreateSnapshot_DuplicateTurnNumber_ThrowsInvalidOperationException()
    {
        // Arrange
        var state = CreateTestState();
        state.CreateSnapshot(1, "First", "user");

        // Act & Assert
        var act = () =>
            state.CreateSnapshot(1, "Duplicate", "user");
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().ContainEquivalentOf("already exists");
    }

    [Fact]
    public void CreateSnapshot_NegativeTurnNumber_ThrowsArgumentException()
    {
        // Arrange
        var state = CreateTestState();

        // Act & Assert
        var act = () =>
            state.CreateSnapshot(-1, "Invalid", "user");
        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.Message.Should().ContainEquivalentOf("cannot be negative");
    }

    [Fact]
    public void RestoreFromSnapshot_WithValidSnapshot_RestoresSuccessfully()
    {
        // Arrange
        var state = CreateTestState();
        var oldState = JsonDocument.Parse("{\"score\":5}");
        state.UpdateState(oldState, "user");
        var snapshot = state.CreateSnapshot(1, "Snapshot", "user");

        var newState = JsonDocument.Parse("{\"score\":10}");
        state.UpdateState(newState, "user");

        // Act
        state.RestoreFromSnapshot(snapshot.Id, "restorer");

        // Assert
        state.Version.Should().Be(4); // Version incremented (initial=1, update=2, update=3, restore=4)
        state.LastUpdatedBy.Should().Be("restorer");
        state.Snapshots.Count.Should().Be(2); // Original + backup snapshot
    }

    [Fact]
    public void RestoreFromSnapshot_InvalidSnapshotId_ThrowsNotFoundException()
    {
        // Arrange
        var state = CreateTestState();

        // Act & Assert
        var act = () =>
            state.RestoreFromSnapshot(Guid.NewGuid(), "user");
        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void GetSnapshotByTurn_ExistingTurn_ReturnsSnapshot()
    {
        // Arrange
        var state = CreateTestState();
        state.CreateSnapshot(1, "Turn 1", "user");

        // Act
        var snapshot = state.GetSnapshotByTurn(1);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.TurnNumber.Should().Be(1);
    }

    [Fact]
    public void GetSnapshotByTurn_NonExistingTurn_ReturnsNull()
    {
        // Arrange
        var state = CreateTestState();

        // Act
        var snapshot = state.GetSnapshotByTurn(999);

        // Assert
        snapshot.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestSnapshot_WithSnapshots_ReturnsNewest()
    {
        // Arrange
        var state = CreateTestState();
        state.CreateSnapshot(1, "First", "user");
        await Task.Delay(TestConstants.Timing.TinyDelay); // Ensure different CreatedAt
        state.CreateSnapshot(2, "Second", "user");

        // Act
        var latest = state.GetLatestSnapshot();

        // Assert
        latest.Should().NotBeNull();
        latest.TurnNumber.Should().Be(2);
        latest.Description.Should().Be("Second");
    }

    [Fact]
    public void GetLatestSnapshot_NoSnapshots_ReturnsNull()
    {
        // Arrange
        var state = CreateTestState();

        // Act
        var latest = state.GetLatestSnapshot();

        // Assert
        latest.Should().BeNull();
    }

    [Fact]
    public void GetStateAsString_ReturnsValidJson()
    {
        // Arrange
        var jsonState = JsonDocument.Parse("{\"player\":\"Alice\",\"score\":42}");
        var state = GameSessionState.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), jsonState, "user");

        // Act
        var jsonString = state.GetStateAsString();

        // Assert
        jsonString.Should().NotBeNull();
        jsonString.Should().Contain("Alice");
        jsonString.Should().Contain("42");
    }

    // Helper method
    private static GameSessionState CreateTestState()
    {
        var initialState = JsonDocument.Parse("{\"score\":0}");
        return GameSessionState.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            initialState,
            "testuser"
        );
    }
}

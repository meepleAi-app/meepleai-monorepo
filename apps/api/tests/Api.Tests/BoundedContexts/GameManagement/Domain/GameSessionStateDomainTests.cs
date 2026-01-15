using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Xunit;

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
        Assert.Equal(stateId, state.Id);
        Assert.Equal(sessionId, state.GameSessionId);
        Assert.Equal(templateId, state.TemplateId);
        Assert.Equal(1, state.Version);
        Assert.Equal("testuser", state.LastUpdatedBy);
        Assert.NotNull(state.CurrentState);
        Assert.Empty(state.Snapshots);
    }

    [Fact]
    public void Create_WithEmptyGameSessionId_ThrowsArgumentException()
    {
        // Arrange
        var initialState = JsonDocument.Parse("{}");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            GameSessionState.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), initialState, "user"));
        Assert.Contains("GameSessionId cannot be empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_WithEmptyTemplateId_ThrowsArgumentException()
    {
        // Arrange
        var initialState = JsonDocument.Parse("{}");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            GameSessionState.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, initialState, "user"));
        Assert.Contains("TemplateId cannot be empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_WithNullInitialState_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            GameSessionState.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null!, "user"));
    }

    [Fact]
    public void Create_WithEmptyCreatedBy_ThrowsArgumentException()
    {
        // Arrange
        var initialState = JsonDocument.Parse("{}");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            GameSessionState.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), initialState, ""));
        Assert.Contains("CreatedBy cannot be empty", exception.Message, StringComparison.OrdinalIgnoreCase);
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
        Assert.Equal(2, state.Version); // Version incremented
        Assert.Equal("updater", state.LastUpdatedBy);
        Assert.NotNull(state.CurrentState);
    }

    [Fact]
    public void CreateSnapshot_WithValidParams_CreatesSuccessfully()
    {
        // Arrange
        var state = CreateTestState();

        // Act
        var snapshot = state.CreateSnapshot(turnNumber: 1, description: "Turn 1 end", createdBy: "user");

        // Assert
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.TurnNumber);
        Assert.Equal("Turn 1 end", snapshot.Description);
        Assert.Equal(1, state.Snapshots.Count);
    }

    [Fact]
    public void CreateSnapshot_DuplicateTurnNumber_ThrowsInvalidOperationException()
    {
        // Arrange
        var state = CreateTestState();
        state.CreateSnapshot(1, "First", "user");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            state.CreateSnapshot(1, "Duplicate", "user"));
        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateSnapshot_NegativeTurnNumber_ThrowsArgumentException()
    {
        // Arrange
        var state = CreateTestState();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            state.CreateSnapshot(-1, "Invalid", "user"));
        Assert.Contains("cannot be negative", exception.Message, StringComparison.OrdinalIgnoreCase);
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
        Assert.Equal(4, state.Version); // Version incremented (initial=1, update=2, update=3, restore=4)
        Assert.Equal("restorer", state.LastUpdatedBy);
        Assert.Equal(2, state.Snapshots.Count); // Original + backup snapshot
    }

    [Fact]
    public void RestoreFromSnapshot_InvalidSnapshotId_ThrowsNotFoundException()
    {
        // Arrange
        var state = CreateTestState();

        // Act & Assert
        Assert.Throws<NotFoundException>(() =>
            state.RestoreFromSnapshot(Guid.NewGuid(), "user"));
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
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.TurnNumber);
    }

    [Fact]
    public void GetSnapshotByTurn_NonExistingTurn_ReturnsNull()
    {
        // Arrange
        var state = CreateTestState();

        // Act
        var snapshot = state.GetSnapshotByTurn(999);

        // Assert
        Assert.Null(snapshot);
    }

    [Fact]
    public void GetLatestSnapshot_WithSnapshots_ReturnsNewest()
    {
        // Arrange
        var state = CreateTestState();
        state.CreateSnapshot(1, "First", "user");
        Thread.Sleep(10); // Ensure different CreatedAt
        state.CreateSnapshot(2, "Second", "user");

        // Act
        var latest = state.GetLatestSnapshot();

        // Assert
        Assert.NotNull(latest);
        Assert.Equal(2, latest.TurnNumber);
        Assert.Equal("Second", latest.Description);
    }

    [Fact]
    public void GetLatestSnapshot_NoSnapshots_ReturnsNull()
    {
        // Arrange
        var state = CreateTestState();

        // Act
        var latest = state.GetLatestSnapshot();

        // Assert
        Assert.Null(latest);
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
        Assert.NotNull(jsonString);
        Assert.Contains("Alice", jsonString);
        Assert.Contains("42", jsonString);
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

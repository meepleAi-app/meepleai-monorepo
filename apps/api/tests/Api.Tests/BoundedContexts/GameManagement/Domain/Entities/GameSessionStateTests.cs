using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.Middleware.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Tests for the GameSessionState aggregate root entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 29
/// </summary>
[Trait("Category", "Unit")]
public sealed class GameSessionStateTests : IDisposable
{
    private readonly List<JsonDocument> _documentsToDispose = new();

    public void Dispose()
    {
        foreach (var doc in _documentsToDispose)
        {
            doc.Dispose();
        }
    }

    #region Create Factory Tests

    [Fact]
    public void Create_WithValidData_ReturnsGameSessionState()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameSessionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var initialState = CreateJsonDocument("{\"score\": 0}");
        var createdBy = "user@example.com";

        // Act
        var state = GameSessionState.Create(id, gameSessionId, templateId, initialState, createdBy);

        // Assert
        state.Id.Should().Be(id);
        state.GameSessionId.Should().Be(gameSessionId);
        state.TemplateId.Should().Be(templateId);
        state.Version.Should().Be(1);
        state.LastUpdatedBy.Should().Be(createdBy);
        state.LastUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        state.Snapshots.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithEmptyGameSessionId_ThrowsArgumentException()
    {
        // Arrange
        var initialState = CreateJsonDocument("{}");

        // Act
        var action = () => GameSessionState.Create(
            Guid.NewGuid(),
            Guid.Empty,
            Guid.NewGuid(),
            initialState,
            "user@example.com");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*GameSessionId cannot be empty*")
            .WithParameterName("gameSessionId");
    }

    [Fact]
    public void Create_WithEmptyTemplateId_ThrowsArgumentException()
    {
        // Arrange
        var initialState = CreateJsonDocument("{}");

        // Act
        var action = () => GameSessionState.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.Empty,
            initialState,
            "user@example.com");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*TemplateId cannot be empty*")
            .WithParameterName("templateId");
    }

    [Fact]
    public void Create_WithNullInitialState_ThrowsArgumentNullException()
    {
        // Act
        var action = () => GameSessionState.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null!,
            "user@example.com");

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("initialState");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyCreatedBy_ThrowsArgumentException(string? createdBy)
    {
        // Arrange
        var initialState = CreateJsonDocument("{}");

        // Act
        var action = () => GameSessionState.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            initialState,
            createdBy!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*CreatedBy cannot be empty*")
            .WithParameterName("createdBy");
    }

    [Fact]
    public void Create_TrimsCreatedBy()
    {
        // Arrange
        var initialState = CreateJsonDocument("{}");

        // Act
        var state = GameSessionState.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            initialState,
            "  user@example.com  ");

        // Assert
        state.LastUpdatedBy.Should().Be("user@example.com");
    }

    [Fact]
    public void Create_AddsDomainEvent()
    {
        // Arrange
        var initialState = CreateJsonDocument("{}");

        // Act
        var state = GameSessionState.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            initialState,
            "user@example.com");

        // Assert
        state.DomainEvents.Should().HaveCount(1);
        state.DomainEvents.First().Should().BeOfType<Api.BoundedContexts.GameManagement.Domain.Events.GameStateInitializedEvent>();
    }

    #endregion

    #region UpdateState Tests

    [Fact]
    public void UpdateState_WithValidData_UpdatesState()
    {
        // Arrange
        var state = CreateValidSessionState();
        var newState = CreateJsonDocument("{\"score\": 100}");
        state.ClearDomainEvents();

        // Act
        state.UpdateState(newState, "updater@example.com");

        // Assert
        state.Version.Should().Be(2);
        state.LastUpdatedBy.Should().Be("updater@example.com");
        state.LastUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void UpdateState_WithNullState_ThrowsArgumentNullException()
    {
        // Arrange
        var state = CreateValidSessionState();

        // Act
        var action = () => state.UpdateState(null!, "user@example.com");

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("newState");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateState_WithEmptyUpdatedBy_ThrowsArgumentException(string? updatedBy)
    {
        // Arrange
        var state = CreateValidSessionState();
        var newState = CreateJsonDocument("{}");

        // Act
        var action = () => state.UpdateState(newState, updatedBy!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*UpdatedBy cannot be empty*")
            .WithParameterName("updatedBy");
    }

    [Fact]
    public void UpdateState_TrimsUpdatedBy()
    {
        // Arrange
        var state = CreateValidSessionState();
        var newState = CreateJsonDocument("{}");

        // Act
        state.UpdateState(newState, "  user@example.com  ");

        // Assert
        state.LastUpdatedBy.Should().Be("user@example.com");
    }

    [Fact]
    public void UpdateState_IncrementsVersion()
    {
        // Arrange
        var state = CreateValidSessionState();
        var originalVersion = state.Version;
        var newState = CreateJsonDocument("{}");

        // Act
        state.UpdateState(newState, "user@example.com");

        // Assert
        state.Version.Should().Be(originalVersion + 1);
    }

    [Fact]
    public void UpdateState_AddsDomainEvent()
    {
        // Arrange
        var state = CreateValidSessionState();
        state.ClearDomainEvents();
        var newState = CreateJsonDocument("{}");

        // Act
        state.UpdateState(newState, "user@example.com");

        // Assert
        state.DomainEvents.Should().HaveCount(1);
        state.DomainEvents.First().Should().BeOfType<Api.BoundedContexts.GameManagement.Domain.Events.GameStateUpdatedEvent>();
    }

    #endregion

    #region CreateSnapshot Tests

    [Fact]
    public void CreateSnapshot_WithValidData_ReturnsSnapshot()
    {
        // Arrange
        var state = CreateValidSessionState();
        state.ClearDomainEvents();

        // Act
        var snapshot = state.CreateSnapshot(1, "Turn 1 completed", "user@example.com");

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.TurnNumber.Should().Be(1);
        snapshot.Description.Should().Be("Turn 1 completed");
        state.Snapshots.Should().HaveCount(1);
        state.Snapshots.Should().Contain(snapshot);
    }

    [Fact]
    public void CreateSnapshot_WithNegativeTurnNumber_ThrowsArgumentException()
    {
        // Arrange
        var state = CreateValidSessionState();

        // Act
        var action = () => state.CreateSnapshot(-1, "Invalid", "user@example.com");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Turn number cannot be negative*")
            .WithParameterName("turnNumber");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateSnapshot_WithEmptyCreatedBy_ThrowsArgumentException(string? createdBy)
    {
        // Arrange
        var state = CreateValidSessionState();

        // Act
        var action = () => state.CreateSnapshot(1, "Description", createdBy!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*CreatedBy cannot be empty*")
            .WithParameterName("createdBy");
    }

    [Fact]
    public void CreateSnapshot_WithDuplicateTurnNumber_ThrowsInvalidOperationException()
    {
        // Arrange
        var state = CreateValidSessionState();
        state.CreateSnapshot(1, "First snapshot", "user@example.com");

        // Act
        var action = () => state.CreateSnapshot(1, "Duplicate turn", "user@example.com");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Snapshot for turn 1 already exists*");
    }

    [Fact]
    public void CreateSnapshot_WithNullDescription_UsesDefaultDescription()
    {
        // Arrange
        var state = CreateValidSessionState();

        // Act
        var snapshot = state.CreateSnapshot(5, null!, "user@example.com");

        // Assert
        snapshot.Description.Should().Be("Snapshot at turn 5");
    }

    [Fact]
    public void CreateSnapshot_AddsDomainEvent()
    {
        // Arrange
        var state = CreateValidSessionState();
        state.ClearDomainEvents();

        // Act
        state.CreateSnapshot(1, "Turn 1", "user@example.com");

        // Assert
        state.DomainEvents.Should().HaveCount(1);
        state.DomainEvents.First().Should().BeOfType<Api.BoundedContexts.GameManagement.Domain.Events.GameStateSnapshotCreatedEvent>();
    }

    #endregion

    #region RestoreFromSnapshot Tests

    [Fact]
    public void RestoreFromSnapshot_WithValidSnapshot_RestoresState()
    {
        // Arrange
        var state = CreateValidSessionState();
        var snapshot = state.CreateSnapshot(1, "Turn 1", "user@example.com");
        var newState = CreateJsonDocument("{\"score\": 500}");
        state.UpdateState(newState, "user@example.com");
        state.ClearDomainEvents();
        var versionBefore = state.Version;

        // Act
        state.RestoreFromSnapshot(snapshot.Id, "restorer@example.com");

        // Assert
        state.Version.Should().BeGreaterThan(versionBefore);
        state.LastUpdatedBy.Should().Be("restorer@example.com");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RestoreFromSnapshot_WithEmptyRestoredBy_ThrowsArgumentException(string? restoredBy)
    {
        // Arrange
        var state = CreateValidSessionState();
        var snapshot = state.CreateSnapshot(1, "Turn 1", "user@example.com");

        // Act
        var action = () => state.RestoreFromSnapshot(snapshot.Id, restoredBy!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*RestoredBy cannot be empty*")
            .WithParameterName("restoredBy");
    }

    [Fact]
    public void RestoreFromSnapshot_WithInvalidSnapshotId_ThrowsNotFoundException()
    {
        // Arrange
        var state = CreateValidSessionState();
        var invalidSnapshotId = Guid.NewGuid();

        // Act
        var action = () => state.RestoreFromSnapshot(invalidSnapshotId, "user@example.com");

        // Assert
        action.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void RestoreFromSnapshot_CreatesBackupSnapshot()
    {
        // Arrange
        var state = CreateValidSessionState();
        var snapshot = state.CreateSnapshot(1, "Turn 1", "user@example.com");
        var initialSnapshotCount = state.Snapshots.Count;

        // Act
        state.RestoreFromSnapshot(snapshot.Id, "restorer@example.com");

        // Assert
        state.Snapshots.Count.Should().BeGreaterThan(initialSnapshotCount);
    }

    [Fact]
    public void RestoreFromSnapshot_AddsDomainEvent()
    {
        // Arrange
        var state = CreateValidSessionState();
        var snapshot = state.CreateSnapshot(1, "Turn 1", "user@example.com");
        state.ClearDomainEvents();

        // Act
        state.RestoreFromSnapshot(snapshot.Id, "restorer@example.com");

        // Assert
        state.DomainEvents.Should().Contain(e => e is Api.BoundedContexts.GameManagement.Domain.Events.GameStateRestoredEvent);
    }

    #endregion

    #region GetSnapshotByTurn Tests

    [Fact]
    public void GetSnapshotByTurn_WithExistingTurn_ReturnsSnapshot()
    {
        // Arrange
        var state = CreateValidSessionState();
        var snapshot = state.CreateSnapshot(3, "Turn 3", "user@example.com");

        // Act
        var result = state.GetSnapshotByTurn(3);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(snapshot.Id);
    }

    [Fact]
    public void GetSnapshotByTurn_WithNonExistingTurn_ReturnsNull()
    {
        // Arrange
        var state = CreateValidSessionState();
        state.CreateSnapshot(1, "Turn 1", "user@example.com");

        // Act
        var result = state.GetSnapshotByTurn(999);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetLatestSnapshot Tests

    [Fact]
    public async Task GetLatestSnapshot_WithMultipleSnapshots_ReturnsLatest()
    {
        // Arrange
        var state = CreateValidSessionState();
        state.CreateSnapshot(1, "Turn 1", "user@example.com");
        await Task.Delay(50); // Ensure different timestamps
        state.CreateSnapshot(2, "Turn 2", "user@example.com");
        await Task.Delay(50);
        var latestSnapshot = state.CreateSnapshot(3, "Turn 3", "user@example.com");

        // Act
        var result = state.GetLatestSnapshot();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(latestSnapshot.Id);
    }

    [Fact]
    public void GetLatestSnapshot_WithNoSnapshots_ReturnsNull()
    {
        // Arrange
        var state = CreateValidSessionState();

        // Act
        var result = state.GetLatestSnapshot();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetStateAsString Tests

    [Fact]
    public void GetStateAsString_ReturnsJsonString()
    {
        // Arrange
        var initialState = CreateJsonDocument("{\"score\":100,\"turn\":5}");
        var state = GameSessionState.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            initialState,
            "user@example.com");

        // Act
        var result = state.GetStateAsString();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("score");
        result.Should().Contain("100");
    }

    #endregion

    #region Internal Constructor Tests

    [Fact]
    public void InternalConstructor_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameSessionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var currentState = CreateJsonDocument("{\"data\": \"test\"}");
        var version = 5;
        var lastUpdatedAt = DateTime.UtcNow.AddHours(-1);
        var lastUpdatedBy = "admin@example.com";
        var snapshots = new List<GameStateSnapshot>();

        // Act
        var state = new GameSessionState(
            id,
            gameSessionId,
            templateId,
            currentState,
            version,
            lastUpdatedAt,
            lastUpdatedBy,
            snapshots);

        // Assert
        state.Id.Should().Be(id);
        state.GameSessionId.Should().Be(gameSessionId);
        state.TemplateId.Should().Be(templateId);
        state.Version.Should().Be(version);
        state.LastUpdatedAt.Should().Be(lastUpdatedAt);
        state.LastUpdatedBy.Should().Be(lastUpdatedBy);
        state.Snapshots.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private GameSessionState CreateValidSessionState()
    {
        var initialState = CreateJsonDocument("{\"score\": 0}");
        return GameSessionState.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            initialState,
            "user@example.com");
    }

    private JsonDocument CreateJsonDocument(string json)
    {
        var doc = JsonDocument.Parse(json);
        _documentsToDispose.Add(doc);
        return doc;
    }

    #endregion
}

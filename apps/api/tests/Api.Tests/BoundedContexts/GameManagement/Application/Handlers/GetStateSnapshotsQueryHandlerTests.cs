using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for GetStateSnapshotsQueryHandler.
/// Issue #2403: GameSessionState Entity (list snapshots for history)
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetStateSnapshotsQueryHandlerTests
{
    private readonly Mock<IGameSessionStateRepository> _stateRepositoryMock;
    private readonly GetStateSnapshotsQueryHandler _handler;

    public GetStateSnapshotsQueryHandlerTests()
    {
        _stateRepositoryMock = new Mock<IGameSessionStateRepository>();
        _handler = new GetStateSnapshotsQueryHandler(_stateRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingStateAndSnapshots_ReturnsSnapshotList()
    {
        // Arrange
        var state = CreateGameSessionStateWithSnapshots();
        var query = new GetStateSnapshotsQuery(state.Id);

        _stateRepositoryMock
            .Setup(r => r.GetByIdAsync(state.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0].TurnNumber);
        Assert.Equal(2, result[1].TurnNumber);
        Assert.Equal(3, result[2].TurnNumber);
    }

    [Fact]
    public async Task Handle_WithExistingStateNoSnapshots_ReturnsEmptyList()
    {
        // Arrange
        var state = CreateGameSessionState();
        var query = new GetStateSnapshotsQuery(state.Id);

        _stateRepositoryMock
            .Setup(r => r.GetByIdAsync(state.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_WithNonExistentState_ThrowsNotFoundException()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var query = new GetStateSnapshotsQuery(stateId);

        _stateRepositoryMock
            .Setup(r => r.GetByIdAsync(stateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSessionState?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(query, TestContext.Current.CancellationToken));

        Assert.Contains("GameSessionState", exception.Message);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var state = CreateGameSessionState();
        var query = new GetStateSnapshotsQuery(state.Id);

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _stateRepositoryMock
            .Setup(r => r.GetByIdAsync(state.Id, token))
            .ReturnsAsync(state);

        // Act
        await _handler.Handle(query, token);

        // Assert
        _stateRepositoryMock.Verify(r => r.GetByIdAsync(state.Id, token), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_MapsSnapshotDtoCorrectly()
    {
        // Arrange
        var state = CreateGameSessionStateWithSnapshots();
        var query = new GetStateSnapshotsQuery(state.Id);

        _stateRepositoryMock
            .Setup(r => r.GetByIdAsync(state.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        var firstSnapshot = result.First();
        Assert.NotEqual(Guid.Empty, firstSnapshot.Id);
        Assert.Equal(1, firstSnapshot.TurnNumber);
        Assert.Equal("Turn 1 snapshot", firstSnapshot.Description);
        Assert.NotNull(firstSnapshot.CreatedAt);
    }

    private static GameSessionState CreateGameSessionState()
    {
        return GameSessionState.Create(
            id: Guid.NewGuid(),
            gameSessionId: Guid.NewGuid(),
            templateId: Guid.NewGuid(),
            initialState: JsonDocument.Parse("{\"turn\": 1}"),
            createdBy: "system"
        );
    }

    private static GameSessionState CreateGameSessionStateWithSnapshots()
    {
        var state = CreateGameSessionState();

        // Create multiple snapshots for testing
        state.CreateSnapshot(turnNumber: 1, description: "Turn 1 snapshot", createdBy: "system");
        state.CreateSnapshot(turnNumber: 2, description: "Turn 2 snapshot", createdBy: "system");
        state.CreateSnapshot(turnNumber: 3, description: "Turn 3 snapshot", createdBy: "system");

        return state;
    }
}

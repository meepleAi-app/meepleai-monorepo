using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for RestoreStateSnapshotCommandHandler.
/// Issue #2403: GameSessionState Entity (restore for undo)
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class RestoreStateSnapshotCommandHandlerTests
{
    private readonly Mock<IGameSessionStateRepository> _stateRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly RestoreStateSnapshotCommandHandler _handler;

    public RestoreStateSnapshotCommandHandlerTests()
    {
        _stateRepositoryMock = new Mock<IGameSessionStateRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new RestoreStateSnapshotCommandHandler(
            _stateRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingStateAndSnapshot_RestoresAndReturnsDto()
    {
        // Arrange
        var state = CreateGameSessionStateWithSnapshot();
        var snapshotId = state.Snapshots.First().Id;
        var command = new RestoreStateSnapshotCommand(
            SessionStateId: state.Id,
            SnapshotId: snapshotId);

        _stateRepositoryMock
            .Setup(r => r.GetByIdAsync(state.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(state.Id, result.Id);

        _stateRepositoryMock.Verify(r => r.UpdateAsync(state, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentState_ThrowsNotFoundException()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();
        var command = new RestoreStateSnapshotCommand(
            SessionStateId: stateId,
            SnapshotId: snapshotId);

        _stateRepositoryMock
            .Setup(r => r.GetByIdAsync(stateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSessionState?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains("GameSessionState", exception.Message);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var state = CreateGameSessionStateWithSnapshot();
        var snapshotId = state.Snapshots.First().Id;
        var command = new RestoreStateSnapshotCommand(
            SessionStateId: state.Id,
            SnapshotId: snapshotId);

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _stateRepositoryMock
            .Setup(r => r.GetByIdAsync(state.Id, token))
            .ReturnsAsync(state);

        // Act
        await _handler.Handle(command, token);

        // Assert
        _stateRepositoryMock.Verify(r => r.GetByIdAsync(state.Id, token), Times.Once);
        _stateRepositoryMock.Verify(r => r.UpdateAsync(state, token), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithNonExistentSnapshot_ThrowsException()
    {
        // Arrange
        var state = CreateGameSessionStateWithSnapshot();
        var nonExistentSnapshotId = Guid.NewGuid();
        var command = new RestoreStateSnapshotCommand(
            SessionStateId: state.Id,
            SnapshotId: nonExistentSnapshotId);

        _stateRepositoryMock
            .Setup(r => r.GetByIdAsync(state.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        // Act & Assert - Domain should throw when snapshot not found
        await Assert.ThrowsAnyAsync<Exception>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    private static GameSessionState CreateGameSessionStateWithSnapshot()
    {
        var state = GameSessionState.Create(
            id: Guid.NewGuid(),
            gameSessionId: Guid.NewGuid(),
            templateId: Guid.NewGuid(),
            initialState: JsonDocument.Parse("{\"turn\": 1}"),
            createdBy: "system"
        );

        // Create a snapshot for testing restore
        state.CreateSnapshot(turnNumber: 1, description: "Initial state", createdBy: "system");

        return state;
    }
}

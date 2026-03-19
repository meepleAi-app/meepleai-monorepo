using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for CreateStateSnapshotCommandHandler.
/// Issue #2403: GameSessionState Entity (snapshots for undo/history)
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreateStateSnapshotCommandHandlerTests
{
    private readonly Mock<IGameSessionStateRepository> _stateRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateStateSnapshotCommandHandler _handler;

    public CreateStateSnapshotCommandHandlerTests()
    {
        _stateRepositoryMock = new Mock<IGameSessionStateRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new CreateStateSnapshotCommandHandler(
            _stateRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingState_CreatesSnapshotAndReturnsDto()
    {
        // Arrange
        var state = CreateGameSessionState();
        var command = new CreateStateSnapshotCommand(
            SessionStateId: state.Id,
            TurnNumber: 5,
            Description: "After turn 5");

        _stateRepositoryMock
            .Setup(r => r.GetByIdAsync(state.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.TurnNumber);
        Assert.Equal("After turn 5", result.Description);

        _stateRepositoryMock.Verify(r => r.UpdateAsync(state, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentState_ThrowsNotFoundException()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var command = new CreateStateSnapshotCommand(
            SessionStateId: stateId,
            TurnNumber: 1,
            Description: "Test");

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
        var state = CreateGameSessionState();
        var command = new CreateStateSnapshotCommand(
            SessionStateId: state.Id,
            TurnNumber: 1,
            Description: "Test");

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
    public async Task Handle_WithEmptyDescription_ThrowsArgumentException()
    {
        // Arrange
        var state = CreateGameSessionState();
        var command = new CreateStateSnapshotCommand(
            SessionStateId: state.Id,
            TurnNumber: 3,
            Description: string.Empty);

        _stateRepositoryMock
            .Setup(r => r.GetByIdAsync(state.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        // Act & Assert
        // Domain validation rejects empty descriptions
        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
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
}

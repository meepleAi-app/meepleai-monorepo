using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for UpdateGameStateCommandHandler.
/// Issue #2403: GameSessionState Entity
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UpdateGameStateCommandHandlerTests
{
    private readonly Mock<IGameSessionStateRepository> _stateRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly UpdateGameStateCommandHandler _handler;

    public UpdateGameStateCommandHandlerTests()
    {
        _stateRepositoryMock = new Mock<IGameSessionStateRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new UpdateGameStateCommandHandler(
            _stateRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingState_UpdatesAndReturnsDto()
    {
        // Arrange
        var state = CreateGameSessionState();
        var newState = JsonDocument.Parse("{\"turn\": 2, \"players\": [\"Alice\", \"Bob\"]}");
        var command = new UpdateGameStateCommand(state.Id, newState);

        _stateRepositoryMock
            .Setup(r => r.GetByIdAsync(state.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(state.Id);
        result.Version.Should().Be(2); // Version incremented

        _stateRepositoryMock.Verify(r => r.UpdateAsync(state, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentState_ThrowsNotFoundException()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var newState = JsonDocument.Parse("{\"turn\": 1}");
        var command = new UpdateGameStateCommand(stateId, newState);

        _stateRepositoryMock
            .Setup(r => r.GetByIdAsync(stateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSessionState?)null);

        // Act & Assert
        var act =
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<NotFoundException>()).Which;

        exception.Message.Should().Contain("GameSessionState");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var state = CreateGameSessionState();
        var newState = JsonDocument.Parse("{\"data\": \"test\"}");
        var command = new UpdateGameStateCommand(state.Id, newState);

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
        var act =
            () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_IncrementsVersionCorrectly()
    {
        // Arrange
        var state = CreateGameSessionState();
        var initialVersion = state.Version;
        var newState = JsonDocument.Parse("{\"updated\": true}");
        var command = new UpdateGameStateCommand(state.Id, newState);

        _stateRepositoryMock
            .Setup(r => r.GetByIdAsync(state.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Version.Should().Be(initialVersion + 1);
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

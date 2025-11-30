using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Comprehensive tests for StartGameSessionCommandHandler.
/// Tests game session creation and lifecycle management.
/// </summary>
public class StartGameSessionCommandHandlerTests
{
    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
    private readonly Mock<IGameRepository> _gameRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly StartGameSessionCommandHandler _handler;

    public StartGameSessionCommandHandlerTests()
    {
        _sessionRepositoryMock = new Mock<IGameSessionRepository>();
        _gameRepositoryMock = new Mock<IGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new StartGameSessionCommandHandler(
            _sessionRepositoryMock.Object,
            _gameRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    #region Happy Path Tests

    [Fact]
    public async Task Handle_WithTwoPlayers_CreatesAndStartsSession()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock
            .Setup(r => r.ExistsAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new StartGameSessionCommand(
            GameId: gameId,
            Players: new List<SessionPlayerRequest>
            {
                new("Alice", 1, "Blue"),
                new("Bob", 2, "Red")
            });

        GameSession? capturedSession = null;
        _sessionRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()))
            .Callback<GameSession, CancellationToken>((s, _) => capturedSession = s)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(gameId, result.GameId);
        Assert.Equal("InProgress", result.Status); // Session is started immediately
        Assert.Equal(2, result.Players.Count);

        // Verify captured session
        Assert.NotNull(capturedSession);
        Assert.Equal(gameId, capturedSession.GameId);

        // Verify repository interactions
        _gameRepositoryMock.Verify(
            r => r.ExistsAsync(gameId, It.IsAny<CancellationToken>()),
            Times.Once);
        _sessionRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithFourPlayers_CreatesSession()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock
            .Setup(r => r.ExistsAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new StartGameSessionCommand(
            GameId: gameId,
            Players: new List<SessionPlayerRequest>
            {
                new("Alice", 1, "Blue"),
                new("Bob", 2, "Red"),
                new("Charlie", 3, "Green"),
                new("Diana", 4, "Yellow")
            });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.Players.Count);
        Assert.Equal("Alice", result.Players[0].PlayerName);
        Assert.Equal("Bob", result.Players[1].PlayerName);
        Assert.Equal("Charlie", result.Players[2].PlayerName);
        Assert.Equal("Diana", result.Players[3].PlayerName);
    }

    [Fact]
    public async Task Handle_WithPlayerColors_AssignsColors()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock
            .Setup(r => r.ExistsAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new StartGameSessionCommand(
            GameId: gameId,
            Players: new List<SessionPlayerRequest>
            {
                new("Player 1", 1, "Red"),
                new("Player 2", 2, "Blue")
            });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Red", result.Players[0].Color);
        Assert.Equal("Blue", result.Players[1].Color);
    }

    [Fact]
    public async Task Handle_GeneratesUniqueSessionId()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock
            .Setup(r => r.ExistsAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new StartGameSessionCommand(
            GameId: gameId,
            Players: new List<SessionPlayerRequest>
            {
                new("Player 1", 1, null),
                new("Player 2", 2, null)
            });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.NotEqual(gameId, result.Id); // Session ID should differ from Game ID
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_NonExistentGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock
            .Setup(r => r.ExistsAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new StartGameSessionCommand(
            GameId: gameId,
            Players: new List<SessionPlayerRequest>
            {
                new("Player 1", 1, null),
                new("Player 2", 2, null)
            });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains($"Game with ID {gameId} not found", exception.Message);

        // Verify session was NOT created
        _sessionRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithPlayerOrdering_PreservesOrder()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock
            .Setup(r => r.ExistsAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new StartGameSessionCommand(
            GameId: gameId,
            Players: new List<SessionPlayerRequest>
            {
                new("Third", 3, null),
                new("First", 1, null),
                new("Second", 2, null)
            });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - Order should be preserved from input
        Assert.Equal("Third", result.Players[0].PlayerName);
        Assert.Equal("First", result.Players[1].PlayerName);
        Assert.Equal("Second", result.Players[2].PlayerName);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepositories()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock
            .Setup(r => r.ExistsAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new StartGameSessionCommand(
            GameId: gameId,
            Players: new List<SessionPlayerRequest>
            {
                new("Player 1", 1, null),
                new("Player 2", 2, null)
            });

        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _gameRepositoryMock.Verify(
            r => r.ExistsAsync(gameId, cancellationToken),
            Times.Once);
        _sessionRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<GameSession>(, TestContext.Current.CancellationToken), cancellationToken),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }

    #endregion

    #region Session Status Tests

    [Fact]
    public async Task Handle_SessionStartsImmediately()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock
            .Setup(r => r.ExistsAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new StartGameSessionCommand(
            GameId: gameId,
            Players: new List<SessionPlayerRequest>
            {
                new("Player 1", 1, null),
                new("Player 2", 2, null)
            });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - Session should be InProgress after Start() is called
        Assert.Equal("InProgress", result.Status);
    }

    #endregion
}


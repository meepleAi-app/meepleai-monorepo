using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.GameManagement.TestHelpers;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Comprehensive tests for AddPlayerToSessionCommandHandler.
/// Tests adding players to sessions with validation and state constraints.
/// </summary>
public class AddPlayerToSessionCommandHandlerTests
{
    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly AddPlayerToSessionCommandHandler _handler;

    public AddPlayerToSessionCommandHandlerTests()
    {
        _sessionRepositoryMock = new Mock<IGameSessionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new AddPlayerToSessionCommandHandler(
            _sessionRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    #region Happy Path Tests

    [Fact]
    public async Task Handle_SetupSession_AddsPlayerSuccessfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithTwoPlayers()
            .Build(); // Setup status

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddPlayerToSessionCommand(
            SessionId: sessionId,
            PlayerName: "Charlie",
            PlayerOrder: 3,
            Color: "Green");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Players.Count);
        Assert.Contains(result.Players, p => p.PlayerName == "Charlie");
        Assert.Contains(result.Players, p => p.Color == "Green");

        _sessionRepositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_InProgressSession_AddsPlayerSuccessfully()
    {
        // Arrange - Players can join mid-game
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithTwoPlayers()
            .ThatIsStarted() // InProgress status
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddPlayerToSessionCommand(
            SessionId: sessionId,
            PlayerName: "Diana",
            PlayerOrder: 3,
            Color: "Yellow");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Players.Count);
        Assert.Contains(result.Players, p => p.PlayerName == "Diana");
        Assert.Equal("InProgress", result.Status);
    }

    [Fact]
    public async Task Handle_PausedSession_AddsPlayerSuccessfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithTwoPlayers()
            .ThatIsStarted()
            .Build();

        session.Pause();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddPlayerToSessionCommand(
            SessionId: sessionId,
            PlayerName: "Eve",
            PlayerOrder: 3);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Players.Count);
        Assert.Equal("Paused", result.Status);
    }

    [Fact]
    public async Task Handle_WithColor_AssignsColor()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithPlayers("Alice", "Bob")
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddPlayerToSessionCommand(
            SessionId: sessionId,
            PlayerName: "Charlie",
            PlayerOrder: 3,
            Color: "Purple");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var newPlayer = result.Players.FirstOrDefault(p => p.PlayerName == "Charlie");
        Assert.NotNull(newPlayer);
        Assert.Equal("Purple", newPlayer.Color);
        Assert.Equal(3, newPlayer.PlayerOrder);
    }

    [Fact]
    public async Task Handle_WithoutColor_AddsPlayerWithNullColor()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithTwoPlayers()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddPlayerToSessionCommand(
            SessionId: sessionId,
            PlayerName: "Frank",
            PlayerOrder: 3,
            Color: null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var newPlayer = result.Players.FirstOrDefault(p => p.PlayerName == "Frank");
        Assert.NotNull(newPlayer);
        Assert.Null(newPlayer.Color);
    }

    [Fact]
    public async Task Handle_AddMultiplePlayers_IncreasesPlayerCount()
    {
        // Arrange - Start with 2 players, add 2 more
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithTwoPlayers()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Add first player
        var command1 = new AddPlayerToSessionCommand(
            SessionId: sessionId,
            PlayerName: "Player 3",
            PlayerOrder: 3);

        await _handler.Handle(command1, CancellationToken.None);

        // Add second player
        var command2 = new AddPlayerToSessionCommand(
            SessionId: sessionId,
            PlayerName: "Player 4",
            PlayerOrder: 4);

        // Act
        var result = await _handler.Handle(command2, CancellationToken.None);

        // Assert
        Assert.Equal(4, result.Players.Count);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_NonExistentSession_ThrowsInvalidOperationException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSession?)null);

        var command = new AddPlayerToSessionCommand(
            SessionId: sessionId,
            PlayerName: "Test",
            PlayerOrder: 1);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains($"Session with ID {sessionId} not found", exception.Message);

        // Verify save was NOT called
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_CompletedSession_ThrowsInvalidOperationException()
    {
        // Arrange - Cannot add players to finished session
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .ThatIsCompleted("Winner")
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddPlayerToSessionCommand(
            SessionId: sessionId,
            PlayerName: "Latecomer",
            PlayerOrder: 5);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("Cannot add player to finished session", exception.Message);
    }

    [Fact]
    public async Task Handle_AbandonedSession_ThrowsInvalidOperationException()
    {
        // Arrange - Cannot add players to abandoned session
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .ThatIsStarted()
            .Build();

        session.Abandon("Test");

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddPlayerToSessionCommand(
            SessionId: sessionId,
            PlayerName: "Latecomer",
            PlayerOrder: 5);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("Cannot add player to finished session", exception.Message);
    }

    [Fact]
    public async Task Handle_DuplicatePlayerName_ThrowsInvalidOperationException()
    {
        // Arrange - Player already exists
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithPlayers("Alice", "Bob")
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddPlayerToSessionCommand(
            SessionId: sessionId,
            PlayerName: "Alice", // Duplicate
            PlayerOrder: 3);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("Player 'Alice' is already in this session", exception.Message);
    }

    [Fact]
    public async Task Handle_DuplicatePlayerNameCaseInsensitive_ThrowsInvalidOperationException()
    {
        // Arrange - Player name check is case-insensitive
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithPlayers("Alice", "Bob")
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddPlayerToSessionCommand(
            SessionId: sessionId,
            PlayerName: "alice", // Different case
            PlayerOrder: 3);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("already in this session", exception.Message);
    }

    #endregion

    #region Player Limit Tests

    [Fact]
    public async Task Handle_SessionWith99Players_CanAddOneMore()
    {
        // Arrange - Just under the 100 player limit
        var sessionId = Guid.NewGuid();
        var players = Enumerable.Range(1, 99)
            .Select(i => new SessionPlayer($"Player {i}", i))
            .ToList();

        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithPlayers(players)
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddPlayerToSessionCommand(
            SessionId: sessionId,
            PlayerName: "Player 100",
            PlayerOrder: 100);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(100, result.Players.Count);
    }

    [Fact]
    public async Task Handle_SessionWith100Players_ThrowsInvalidOperationException()
    {
        // Arrange - At the 100 player limit
        var sessionId = Guid.NewGuid();
        var players = Enumerable.Range(1, 100)
            .Select(i => new SessionPlayer($"Player {i}", i))
            .ToList();

        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithPlayers(players)
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddPlayerToSessionCommand(
            SessionId: sessionId,
            PlayerName: "Player 101",
            PlayerOrder: 101);

        // Act & Assert - Domain validation throws ValidationException for player order > 100
        var exception = await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("Player order cannot exceed 100", exception.Message);
    }

    #endregion

    #region Domain Behavior Tests

    [Fact]
    public async Task Handle_PreservesSessionMetadata()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithGameId(gameId)
            .ThatIsStarted()
            .Build();

        var originalStartedAt = session.StartedAt;
        var originalStatus = session.Status.Value;

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddPlayerToSessionCommand(
            SessionId: sessionId,
            PlayerName: "New Player",
            PlayerOrder: 3);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - All metadata should be preserved
        Assert.Equal(sessionId.ToString(), result.Id.ToString());
        Assert.Equal(gameId.ToString(), result.GameId.ToString());
        Assert.Equal(originalStartedAt, result.StartedAt);
        Assert.Equal(originalStatus, result.Status);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithTwoPlayers()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddPlayerToSessionCommand(
            SessionId: sessionId,
            PlayerName: "Test Player",
            PlayerOrder: 3);

        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _sessionRepositoryMock.Verify(
            r => r.GetByIdAsync(sessionId, cancellationToken),
            Times.Once);
        _sessionRepositoryMock.Verify(
            r => r.UpdateAsync(session, cancellationToken),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }

    #endregion
}

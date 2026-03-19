using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Comprehensive tests for StartGameSessionCommandHandler.
/// Tests game session creation and lifecycle management.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class StartGameSessionCommandHandlerTests
{
    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
    private readonly Mock<IGameRepository> _gameRepositoryMock;
    private readonly Mock<ISessionQuotaService> _quotaServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly StartGameSessionCommandHandler _handler;

    // Default test user values (Issue #3070)
    private static readonly Guid DefaultUserId = Guid.NewGuid();
    private static readonly UserTier DefaultUserTier = UserTier.Normal;
    private static readonly Role DefaultUserRole = Role.User;

    public StartGameSessionCommandHandlerTests()
    {
        _sessionRepositoryMock = new Mock<IGameSessionRepository>();
        _gameRepositoryMock = new Mock<IGameRepository>();
        _quotaServiceMock = new Mock<ISessionQuotaService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        // Setup quota service to allow by default (Issue #3070)
        _quotaServiceMock
            .Setup(s => s.CheckQuotaAsync(It.IsAny<Guid>(), It.IsAny<UserTier>(), It.IsAny<Role>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionQuotaResult.Allowed(currentCount: 0, maxAllowed: 10));

        _handler = new StartGameSessionCommandHandler(
            _sessionRepositoryMock.Object,
            _gameRepositoryMock.Object,
            _quotaServiceMock.Object,
            _unitOfWorkMock.Object);
    }
    [Fact]
    public async Task Handle_WithTwoPlayers_CreatesAndStartsSession()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = CreateTestGame(gameId);
        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = CreateCommand(
            gameId: gameId,
            players: new List<SessionPlayerRequest>
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
            r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()),
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
        var game = CreateTestGame(gameId, minPlayers: 2, maxPlayers: 6);
        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = CreateCommand(
            gameId: gameId,
            players: new List<SessionPlayerRequest>
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
        var game = CreateTestGame(gameId);
        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = CreateCommand(
            gameId: gameId,
            players: new List<SessionPlayerRequest>
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
        var game = CreateTestGame(gameId);
        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = CreateCommand(
            gameId: gameId,
            players: new List<SessionPlayerRequest>
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

    // ===== VALIDATION TESTS =====

    [Fact]
    public async Task Handle_EmptyGameId_ThrowsValidationException()
    {
        // Arrange
        var command = CreateCommand(
            gameId: Guid.Empty,
            players: new List<SessionPlayerRequest>
            {
                new("Player 1", 1)
            });

        // Act & Assert
        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        _sessionRepositoryMock.Verify(r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmptyPlayersList_ThrowsValidationException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = CreateTestGame(gameId);

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = CreateCommand(
            gameId: gameId,
            players: new List<SessionPlayerRequest>());

        // Act & Assert
        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        _sessionRepositoryMock.Verify(r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PlayerCountExceedsGameMaximum_ThrowsValidationException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = CreateTestGame(gameId, minPlayers: 2, maxPlayers: 4);

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // 5 players when game max is 4
        var command = CreateCommand(
            gameId: gameId,
            players: new List<SessionPlayerRequest>
            {
                new("Player 1", 1),
                new("Player 2", 2),
                new("Player 3", 3),
                new("Player 4", 4),
                new("Player 5", 5)
            });

        // Act & Assert
        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        _sessionRepositoryMock.Verify(r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Game CreateTestGame(Guid id, int minPlayers = 2, int maxPlayers = 4)
    {
        var title = new GameTitle("Test Game");
        var playerCount = new PlayerCount(minPlayers, maxPlayers);
        return new Game(
            id: id,
            title: title,
            publisher: null,
            yearPublished: null,
            playerCount: playerCount,
            playTime: null
        );
    }

    /// <summary>
    /// Helper method to create StartGameSessionCommand with required parameters (Issue #3070).
    /// </summary>
    private static StartGameSessionCommand CreateCommand(
        Guid gameId,
        IEnumerable<SessionPlayerRequest> players,
        Guid? userId = null,
        UserTier? userTier = null,
        Role? userRole = null)
    {
        return new StartGameSessionCommand(
            GameId: gameId,
            Players: players.ToList().AsReadOnly(),
            UserId: userId ?? DefaultUserId,
            UserTier: userTier ?? DefaultUserTier,
            UserRole: userRole ?? DefaultUserRole);
    }

    [Fact]
    public async Task Handle_NonExistentGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        var command = CreateCommand(
            gameId: gameId,
            players: new List<SessionPlayerRequest>
            {
                new("Player 1", 1, null),
                new("Player 2", 2, null)
            });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains($"Game with ID {gameId} not found", exception.Message, StringComparison.OrdinalIgnoreCase);

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
        var game = CreateTestGame(gameId, minPlayers: 2, maxPlayers: 6);
        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = CreateCommand(
            gameId: gameId,
            players: new List<SessionPlayerRequest>
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
    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepositories()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = CreateTestGame(gameId);
        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = CreateCommand(
            gameId: gameId,
            players: new List<SessionPlayerRequest>
            {
                new("Player 1", 1, null),
                new("Player 2", 2, null)
            });

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _gameRepositoryMock.Verify(
            r => r.GetByIdAsync(gameId, cancellationToken),
            Times.Once);
        _sessionRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<GameSession>(), cancellationToken),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }
    [Fact]
    public async Task Handle_SessionStartsImmediately()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = CreateTestGame(gameId);
        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = CreateCommand(
            gameId: gameId,
            players: new List<SessionPlayerRequest>
            {
                new("Player 1", 1, null),
                new("Player 2", 2, null)
            });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - Session should be InProgress after Start() is called
        Assert.Equal("InProgress", result.Status);
    }
}


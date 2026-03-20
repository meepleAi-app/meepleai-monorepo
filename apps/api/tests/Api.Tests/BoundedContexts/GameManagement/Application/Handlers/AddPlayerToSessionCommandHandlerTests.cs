using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.GameManagement.TestHelpers;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Comprehensive tests for AddPlayerToSessionCommandHandler.
/// Tests adding players to sessions with validation and state constraints.
/// </summary>
[Trait("Category", TestCategories.Unit)]
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
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Players.Count.Should().Be(3);
        result.Players.Should().Contain(p => p.PlayerName == "Charlie");
        result.Players.Should().Contain(p => p.Color == "Green");

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
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Players.Count.Should().Be(3);
        result.Players.Should().Contain(p => p.PlayerName == "Diana");
        result.Status.Should().Be("InProgress");
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
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Players.Count.Should().Be(3);
        result.Status.Should().Be("Paused");
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
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var newPlayer = result.Players.FirstOrDefault(p => p.PlayerName == "Charlie");
        newPlayer.Should().NotBeNull();
        newPlayer.Color.Should().Be("Purple");
        newPlayer.PlayerOrder.Should().Be(3);
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
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var newPlayer = result.Players.FirstOrDefault(p => p.PlayerName == "Frank");
        newPlayer.Should().NotBeNull();
        newPlayer.Color.Should().BeNull();
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

        await _handler.Handle(command1, TestContext.Current.CancellationToken);

        // Add second player
        var command2 = new AddPlayerToSessionCommand(
            SessionId: sessionId,
            PlayerName: "Player 4",
            PlayerOrder: 4);

        // Act
        var result = await _handler.Handle(command2, TestContext.Current.CancellationToken);

        // Assert
        result.Players.Count.Should().Be(4);
    }
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
        var act =
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().ContainEquivalentOf($"Session with ID {sessionId} not found");

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
        var act =
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().ContainEquivalentOf("Cannot add player to finished session");
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
        var act =
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().ContainEquivalentOf("Cannot add player to finished session");
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
        var act =
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().ContainEquivalentOf("Player 'Alice' is already in this session");
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
        var act =
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().ContainEquivalentOf("already in this session");
    }
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
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Players.Count.Should().Be(100);
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
        var act =
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>()).Which;

        exception.Message.Should().ContainEquivalentOf("Player order cannot exceed 100");
    }
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
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - All metadata should be preserved
        result.Id.ToString().Should().Be(sessionId.ToString());
        result.GameId.ToString().Should().Be(gameId.ToString());
        result.StartedAt.Should().Be(originalStartedAt);
        result.Status.Should().Be(originalStatus);
    }
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

        using var cts = new CancellationTokenSource();
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
}


using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.GameManagement.TestHelpers;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Comprehensive tests for EndGameSessionCommandHandler.
/// Tests game session completion with winner recording.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class EndGameSessionCommandHandlerTests
{
    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly EndGameSessionCommandHandler _handler;

    public EndGameSessionCommandHandlerTests()
    {
        _sessionRepositoryMock = new Mock<IGameSessionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new EndGameSessionCommandHandler(
            _sessionRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }
    [Fact]
    public async Task Handle_WithWinner_CompletesSessionAndRecordsWinner()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithPlayers("Alice", "Bob", "Charlie")
            .ThatIsStarted() // Must be InProgress to complete
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new EndGameSessionCommand(
            SessionId: sessionId,
            WinnerName: "Alice");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(sessionId);
        result.Status.Should().Be("Completed");
        result.WinnerName.Should().Be("Alice");
        result.CompletedAt.Should().NotBeNull();

        // Verify repository interactions
        _sessionRepositoryMock.Verify(
            r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
        _sessionRepositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutWinner_CompletesSessionWithNullWinner()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithPlayers("Player 1", "Player 2")
            .ThatIsStarted()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new EndGameSessionCommand(
            SessionId: sessionId,
            WinnerName: null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Completed");
        result.WinnerName.Should().BeNull(); // No winner specified (e.g., cooperative game)
        result.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithFourPlayerGame_RecordsWinner()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithFourPlayers() // Alice, Bob, Charlie, Diana
            .ThatIsStarted()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new EndGameSessionCommand(
            SessionId: sessionId,
            WinnerName: "Charlie");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.WinnerName.Should().Be("Charlie");
        result.Players.Count.Should().Be(4);
    }

    [Fact]
    public async Task Handle_PreservesStartTime()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithPlayers("Player 1", "Player 2")
            .ThatIsStarted()
            .Build();

        var startedAt = session.StartedAt;

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new EndGameSessionCommand(
            SessionId: sessionId,
            WinnerName: "Player 1");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.StartedAt.Should().Be(startedAt); // StartedAt should be preserved
        result.CompletedAt.Should().NotBeNull();
        (result.CompletedAt >= result.StartedAt).Should().BeTrue(); // EndedAt should be after StartedAt
    }
    [Fact]
    public async Task Handle_NonExistentSession_ThrowsInvalidOperationException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSession?)null);

        var command = new EndGameSessionCommand(
            SessionId: sessionId,
            WinnerName: "Player 1");

        // Act & Assert
        var act = 
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().ContainEquivalentOf($"Session with ID {sessionId} not found");

        // Verify update was NOT called
        _sessionRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepositories()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithPlayers("Player 1", "Player 2")
            .ThatIsStarted()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new EndGameSessionCommand(
            SessionId: sessionId,
            WinnerName: "Player 1");

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
    [Fact]
    public async Task Handle_PreservesGameId()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithGameId(gameId)
            .WithPlayers("Player 1", "Player 2")
            .ThatIsStarted()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new EndGameSessionCommand(
            SessionId: sessionId,
            WinnerName: "Player 1");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.GameId.Should().Be(gameId);
    }

    [Fact]
    public async Task Handle_PreservesPlayerList()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithPlayers("Alice", "Bob", "Charlie")
            .ThatIsStarted()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new EndGameSessionCommand(
            SessionId: sessionId,
            WinnerName: "Bob");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Players.Count.Should().Be(3);
        result.Players[0].PlayerName.Should().Be("Alice");
        result.Players[1].PlayerName.Should().Be("Bob");
        result.Players[2].PlayerName.Should().Be("Charlie");
    }
}


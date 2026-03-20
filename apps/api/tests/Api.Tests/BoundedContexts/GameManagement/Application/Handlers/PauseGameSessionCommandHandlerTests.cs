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
/// Comprehensive tests for PauseGameSessionCommandHandler.
/// Tests session pause functionality with state transition validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class PauseGameSessionCommandHandlerTests
{
    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly PauseGameSessionCommandHandler _handler;

    public PauseGameSessionCommandHandlerTests()
    {
        _sessionRepositoryMock = new Mock<IGameSessionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new PauseGameSessionCommandHandler(
            _sessionRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }
    [Fact]
    public async Task Handle_InProgressSession_PausesSuccessfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .ThatIsStarted() // InProgress status
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new PauseGameSessionCommand(SessionId: sessionId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Paused");
        result.Id.ToString().Should().Be(sessionId.ToString());

        _sessionRepositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SessionWithTwoPlayers_PausesAndPreservesPlayers()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithTwoPlayers()
            .ThatIsStarted()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new PauseGameSessionCommand(SessionId: sessionId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be("Paused");
        result.Players.Count.Should().Be(2);
        result.Players[0].PlayerName.Should().Be("Player 1");
        result.Players[1].PlayerName.Should().Be("Player 2");
    }

    [Fact]
    public async Task Handle_SessionWithFourPlayers_PausesAndPreservesAllPlayers()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithFourPlayers()
            .ThatIsStarted()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new PauseGameSessionCommand(SessionId: sessionId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be("Paused");
        result.Players.Count.Should().Be(4);
    }

    [Fact]
    public async Task Handle_SessionWithNotes_PreservesNotes()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithNotes("Game is going well!")
            .ThatIsStarted()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new PauseGameSessionCommand(SessionId: sessionId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be("Paused");
        result.Notes.Should().Contain("Game is going well!");
    }
    [Fact]
    public async Task Handle_NonExistentSession_ThrowsInvalidOperationException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSession?)null);

        var command = new PauseGameSessionCommand(SessionId: sessionId);

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
    public async Task Handle_SessionInSetupStatus_ThrowsInvalidOperationException()
    {
        // Arrange - Session not started yet (Setup status)
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .Build(); // Default is Setup status

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new PauseGameSessionCommand(SessionId: sessionId);

        // Act & Assert
        var act = 
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().ContainEquivalentOf("Cannot pause session in Setup status");

        // Verify save was NOT called
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_CompletedSession_ThrowsInvalidOperationException()
    {
        // Arrange - Session already completed
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .ThatIsCompleted("Winner")
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new PauseGameSessionCommand(SessionId: sessionId);

        // Act & Assert
        var act = 
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().ContainEquivalentOf("Cannot pause session in Completed status");
    }

    [Fact]
    public async Task Handle_AlreadyPausedSession_ThrowsInvalidOperationException()
    {
        // Arrange - Session already paused
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .ThatIsStarted()
            .Build();

        // Pause it first
        session.Pause();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new PauseGameSessionCommand(SessionId: sessionId);

        // Act & Assert
        var act = 
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().ContainEquivalentOf("Cannot pause session in Paused status");
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
        var originalPlayerCount = session.PlayerCount;

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new PauseGameSessionCommand(SessionId: sessionId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - All metadata except status should be unchanged
        result.Id.ToString().Should().Be(sessionId.ToString());
        result.GameId.ToString().Should().Be(gameId.ToString());
        result.StartedAt.Should().Be(originalStartedAt);
        result.Players.Count.Should().Be(originalPlayerCount);
        result.CompletedAt.Should().BeNull(); // Still not completed
        result.WinnerName.Should().BeNull();
    }
    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .ThatIsStarted()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new PauseGameSessionCommand(SessionId: sessionId);

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


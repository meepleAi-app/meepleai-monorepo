using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.GameManagement.TestHelpers;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Comprehensive tests for ResumeGameSessionCommandHandler.
/// Tests session resume functionality with state transition validation.
/// </summary>
public class ResumeGameSessionCommandHandlerTests
{
    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ResumeGameSessionCommandHandler _handler;

    public ResumeGameSessionCommandHandlerTests()
    {
        _sessionRepositoryMock = new Mock<IGameSessionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new ResumeGameSessionCommandHandler(
            _sessionRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    #region Happy Path Tests

    [Fact]
    public async Task Handle_PausedSession_ResumesSuccessfully()
    {
        // Arrange
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

        var command = new ResumeGameSessionCommand(SessionId: sessionId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("InProgress", result.Status);
        Assert.Equal(sessionId.ToString(), result.Id.ToString());

        _sessionRepositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PausedSessionWithTwoPlayers_ResumesAndPreservesPlayers()
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

        var command = new ResumeGameSessionCommand(SessionId: sessionId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("InProgress", result.Status);
        Assert.Equal(2, result.Players.Count);
        Assert.Equal("Player 1", result.Players[0].PlayerName);
        Assert.Equal("Player 2", result.Players[1].PlayerName);
    }

    [Fact]
    public async Task Handle_PausedSessionWithFourPlayers_ResumesAndPreservesAllPlayers()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithFourPlayers()
            .ThatIsStarted()
            .Build();

        session.Pause();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new ResumeGameSessionCommand(SessionId: sessionId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("InProgress", result.Status);
        Assert.Equal(4, result.Players.Count);
    }

    [Fact]
    public async Task Handle_PausedSessionWithNotes_PreservesNotes()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithNotes("Taking a break")
            .ThatIsStarted()
            .Build();

        session.Pause();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new ResumeGameSessionCommand(SessionId: sessionId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("InProgress", result.Status);
        Assert.Contains("Taking a break", result.Notes);
    }

    [Fact]
    public async Task Handle_MultiplePauseResumeSequence_WorksCorrectly()
    {
        // Arrange - Simulate pause-resume-pause-resume sequence
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .ThatIsStarted()
            .Build();

        // First pause-resume cycle
        session.Pause();
        session.Resume();

        // Second pause
        session.Pause();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new ResumeGameSessionCommand(SessionId: sessionId);

        // Act - Resume from second pause
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("InProgress", result.Status);
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

        var command = new ResumeGameSessionCommand(SessionId: sessionId);

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

        var command = new ResumeGameSessionCommand(SessionId: sessionId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("Cannot resume session in Setup status", exception.Message);

        // Verify save was NOT called
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AlreadyInProgressSession_ThrowsInvalidOperationException()
    {
        // Arrange - Session already in progress (not paused)
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .ThatIsStarted() // InProgress status
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new ResumeGameSessionCommand(SessionId: sessionId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("Cannot resume session in InProgress status", exception.Message);
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

        var command = new ResumeGameSessionCommand(SessionId: sessionId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("Cannot resume session in Completed status", exception.Message);
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

        session.Pause();

        var originalStartedAt = session.StartedAt;
        var originalPlayerCount = session.PlayerCount;

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new ResumeGameSessionCommand(SessionId: sessionId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - All metadata except status should be unchanged
        Assert.Equal(sessionId.ToString(), result.Id.ToString());
        Assert.Equal(gameId.ToString(), result.GameId.ToString());
        Assert.Equal(originalStartedAt, result.StartedAt);
        Assert.Equal(originalPlayerCount, result.Players.Count);
        Assert.Null(result.CompletedAt); // Still not completed
        Assert.Null(result.WinnerName);
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
            .ThatIsStarted()
            .Build();

        session.Pause();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new ResumeGameSessionCommand(SessionId: sessionId);

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

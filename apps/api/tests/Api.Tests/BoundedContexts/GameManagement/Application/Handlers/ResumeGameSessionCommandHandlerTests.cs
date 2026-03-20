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
/// Comprehensive tests for ResumeGameSessionCommandHandler.
/// Tests session resume functionality with state transition validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
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
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("InProgress");
        result.Id.ToString().Should().Be(sessionId.ToString());

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
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be("InProgress");
        result.Players.Count.Should().Be(2);
        result.Players[0].PlayerName.Should().Be("Player 1");
        result.Players[1].PlayerName.Should().Be("Player 2");
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
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be("InProgress");
        result.Players.Count.Should().Be(4);
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
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be("InProgress");
        result.Notes.Should().Contain("Taking a break");
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
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be("InProgress");
    }
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

        var command = new ResumeGameSessionCommand(SessionId: sessionId);

        // Act & Assert
        var act = 
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().ContainEquivalentOf("Cannot resume session in Setup status");

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
        var act = 
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().ContainEquivalentOf("Cannot resume session in InProgress status");
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
        var act = 
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().ContainEquivalentOf("Cannot resume session in Completed status");
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

        session.Pause();

        var originalStartedAt = session.StartedAt;
        var originalPlayerCount = session.PlayerCount;

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new ResumeGameSessionCommand(SessionId: sessionId);

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

        session.Pause();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new ResumeGameSessionCommand(SessionId: sessionId);

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


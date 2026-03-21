using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class SessionTimerActionCommandHandlerAdditionalTests
{
    private readonly TimerStateManager _timerManager = new();
    private readonly Mock<ILogger<SessionTimerActionCommandHandler>> _loggerMock = new();
    private readonly SessionTimerActionCommandHandler _handler;

    public SessionTimerActionCommandHandlerAdditionalTests()
    {
        _handler = new SessionTimerActionCommandHandler(_timerManager, _loggerMock.Object);
    }

    [Fact]
    public void Constructor_NullTimerManager_ThrowsArgumentNullException()
    {
        var act = () => new SessionTimerActionCommandHandler(
            null!,
            _loggerMock.Object);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new SessionTimerActionCommandHandler(
            _timerManager,
            null!);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public async Task Handle_StartAction_CreatesTimerAndReturnsResult()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        var command = new SessionTimerActionCommand(
            sessionId,
            participantId,
            participantId,
            TimerAction.Start,
            "Player1",
            60);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(TimerAction.Start, result.Action);
        Assert.Equal("running", result.Status);
        Assert.Equal(60, result.RemainingSeconds);
    }

    [Fact]
    public async Task Handle_PauseWithNoRunningTimer_ThrowsConflictException()
    {
        // Arrange — no timer started for this session
        var command = new SessionTimerActionCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            TimerAction.Pause);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ResumeWithNoPausedTimer_ThrowsConflictException()
    {
        // Arrange — no timer started for this session
        var command = new SessionTimerActionCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            TimerAction.Resume);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PauseAfterStart_PausesTimer()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        var startCommand = new SessionTimerActionCommand(
            sessionId, participantId, participantId, TimerAction.Start, "Player1", 120);
        await _handler.Handle(startCommand, CancellationToken.None);

        var pauseCommand = new SessionTimerActionCommand(
            sessionId, participantId, participantId, TimerAction.Pause);

        // Act
        var result = await _handler.Handle(pauseCommand, CancellationToken.None);

        // Assert
        Assert.Equal(TimerAction.Pause, result.Action);
        Assert.Equal("paused", result.Status);
    }

    [Fact]
    public async Task Handle_ResetAction_RemovesTimerAndReturnsIdle()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        // Start a timer first
        var startCommand = new SessionTimerActionCommand(
            sessionId, participantId, participantId, TimerAction.Start, "Player1", 60);
        await _handler.Handle(startCommand, CancellationToken.None);

        var resetCommand = new SessionTimerActionCommand(
            sessionId, participantId, participantId, TimerAction.Reset);

        // Act
        var result = await _handler.Handle(resetCommand, CancellationToken.None);

        // Assert
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(TimerAction.Reset, result.Action);
        Assert.Equal("idle", result.Status);
        Assert.Equal(0, result.RemainingSeconds);
    }

    [Fact]
    public async Task Handle_ResumeAfterPause_ResumesTimer()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        await _handler.Handle(new SessionTimerActionCommand(
            sessionId, participantId, participantId, TimerAction.Start, "Player1", 60), CancellationToken.None);
        await _handler.Handle(new SessionTimerActionCommand(
            sessionId, participantId, participantId, TimerAction.Pause), CancellationToken.None);

        var resumeCommand = new SessionTimerActionCommand(
            sessionId, participantId, participantId, TimerAction.Resume);

        // Act
        var result = await _handler.Handle(resumeCommand, CancellationToken.None);

        // Assert
        Assert.Equal(TimerAction.Resume, result.Action);
        Assert.Equal("running", result.Status);
    }
}

using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.Middleware.Exceptions;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Handles the unified timer action command (Start/Pause/Resume/Reset).
/// Delegates to the existing TimerStateManager in-memory state for each action.
/// Issue #4765 - Player Action Endpoints
/// </summary>
public class SessionTimerActionCommandHandler : IRequestHandler<SessionTimerActionCommand, SessionTimerActionResult>
{
    private readonly TimerStateManager _timerManager;
    private readonly ILogger<SessionTimerActionCommandHandler> _logger;

    public SessionTimerActionCommandHandler(
        TimerStateManager timerManager,
        ILogger<SessionTimerActionCommandHandler> logger)
    {
        _timerManager = timerManager ?? throw new ArgumentNullException(nameof(timerManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<SessionTimerActionResult> Handle(SessionTimerActionCommand request, CancellationToken cancellationToken)
    {
        return request.Action switch
        {
            TimerAction.Start => HandleStart(request),
            TimerAction.Pause => HandlePause(request),
            TimerAction.Resume => HandleResume(request),
            TimerAction.Reset => HandleReset(request),
            _ => throw new InvalidOperationException($"Unknown timer action: {request.Action}")
        };
    }

    private Task<SessionTimerActionResult> HandleStart(SessionTimerActionCommand request)
    {
        var timer = _timerManager.CreateTimer(
            request.SessionId,
            request.DurationSeconds,
            request.ParticipantId,
            request.ParticipantName);

        _logger.LogInformation(
            "Timer started for session {SessionId}: {Duration}s by participant {ParticipantId}",
            request.SessionId, request.DurationSeconds, request.ParticipantId);

        return Task.FromResult(new SessionTimerActionResult(
            request.SessionId,
            TimerAction.Start,
            timer.Status,
            timer.RemainingSeconds,
            timer.StartedAt!.Value));
    }

    private Task<SessionTimerActionResult> HandlePause(SessionTimerActionCommand request)
    {
        var timer = _timerManager.GetTimer(request.SessionId);
        if (timer == null || !string.Equals(timer.Status, "running", StringComparison.Ordinal))
            throw new ConflictException("No running timer found for this session.");

        timer.RemainingSeconds = _timerManager.CalculateRemainingSeconds(timer);
        timer.Status = "paused";
        timer.PausedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Timer paused for session {SessionId}: {Remaining}s remaining",
            request.SessionId, timer.RemainingSeconds);

        return Task.FromResult(new SessionTimerActionResult(
            request.SessionId,
            TimerAction.Pause,
            timer.Status,
            timer.RemainingSeconds,
            DateTime.UtcNow));
    }

    private Task<SessionTimerActionResult> HandleResume(SessionTimerActionCommand request)
    {
        var timer = _timerManager.GetTimer(request.SessionId);
        if (timer == null || !string.Equals(timer.Status, "paused", StringComparison.Ordinal))
            throw new ConflictException("No paused timer found for this session.");

        timer.Status = "running";
        timer.LastTickAt = DateTime.UtcNow;
        timer.PausedAt = null;

        _logger.LogInformation(
            "Timer resumed for session {SessionId}: {Remaining}s remaining",
            request.SessionId, timer.RemainingSeconds);

        return Task.FromResult(new SessionTimerActionResult(
            request.SessionId,
            TimerAction.Resume,
            timer.Status,
            timer.RemainingSeconds,
            DateTime.UtcNow));
    }

    private Task<SessionTimerActionResult> HandleReset(SessionTimerActionCommand request)
    {
        _timerManager.RemoveTimer(request.SessionId);

        _logger.LogInformation("Timer reset for session {SessionId}", request.SessionId);

        return Task.FromResult(new SessionTimerActionResult(
            request.SessionId,
            TimerAction.Reset,
            "idle",
            0,
            DateTime.UtcNow));
    }
}

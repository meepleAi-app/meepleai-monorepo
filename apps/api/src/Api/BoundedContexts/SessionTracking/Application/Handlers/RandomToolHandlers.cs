// Handlers for random tool commands (Issue #3345).
// Uses in-memory state for timer (session-scoped, no persistence needed).

using System.Collections.Concurrent;
using System.Security.Cryptography;

using Api.BoundedContexts.SessionTracking.Application.Commands;

using MediatR;

using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

// ============================================================================
// Timer State Management (In-Memory)
// ============================================================================

/// <summary>
/// In-memory timer state for sessions.
/// </summary>
public sealed class SessionTimerState
{
    public Guid TimerId { get; set; }
    public int DurationSeconds { get; set; }
    public int RemainingSeconds { get; set; }
    public string Status { get; set; } = "idle";
    public Guid StartedBy { get; set; }
    public string StartedByName { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? PausedAt { get; set; }
    public DateTime? LastTickAt { get; set; }
}

/// <summary>
/// Timer state manager (singleton).
/// </summary>
public sealed class TimerStateManager
{
    private readonly ConcurrentDictionary<Guid, SessionTimerState> _timers = new();

    public SessionTimerState? GetTimer(Guid sessionId) =>
        _timers.TryGetValue(sessionId, out var timer) ? timer : null;

    public SessionTimerState CreateTimer(
        Guid sessionId,
        int durationSeconds,
        Guid startedBy,
        string startedByName)
    {
        var timer = new SessionTimerState
        {
            TimerId = Guid.NewGuid(),
            DurationSeconds = durationSeconds,
            RemainingSeconds = durationSeconds,
            Status = "running",
            StartedBy = startedBy,
            StartedByName = startedByName,
            StartedAt = DateTime.UtcNow,
            LastTickAt = DateTime.UtcNow
        };

        _timers[sessionId] = timer;
        return timer;
    }

    public void RemoveTimer(Guid sessionId) =>
        _timers.TryRemove(sessionId, out _);

    public int CalculateRemainingSeconds(SessionTimerState timer)
    {
        if (!string.Equals(timer.Status, "running", StringComparison.Ordinal) || timer.LastTickAt == null)
            return timer.RemainingSeconds;

        var elapsed = (int)(DateTime.UtcNow - timer.LastTickAt.Value).TotalSeconds;
        return Math.Max(0, timer.RemainingSeconds - elapsed);
    }
}

// ============================================================================
// Timer Handlers
// ============================================================================

public sealed class StartTimerCommandHandler : IRequestHandler<StartTimerCommand, StartTimerResponse>
{
    private readonly TimerStateManager _timerManager;
    private readonly ILogger<StartTimerCommandHandler> _logger;

    public StartTimerCommandHandler(
        TimerStateManager timerManager,
        ILogger<StartTimerCommandHandler> logger)
    {
        _timerManager = timerManager;
        _logger = logger;
    }

    public Task<StartTimerResponse> Handle(StartTimerCommand request, CancellationToken cancellationToken)
    {
        var timer = _timerManager.CreateTimer(
            request.SessionId,
            request.DurationSeconds,
            request.ParticipantId,
            request.ParticipantName);

        _logger.LogInformation(
            "Timer started for session {SessionId}: {Duration}s by {ParticipantName}",
            request.SessionId,
            request.DurationSeconds,
            request.ParticipantName);

        return Task.FromResult(new StartTimerResponse(
            request.SessionId,
            timer.TimerId,
            timer.DurationSeconds,
            timer.StartedAt!.Value));
    }
}

public sealed class PauseTimerCommandHandler : IRequestHandler<PauseTimerCommand, TimerStatusResponse>
{
    private readonly TimerStateManager _timerManager;
    private readonly ILogger<PauseTimerCommandHandler> _logger;

    public PauseTimerCommandHandler(
        TimerStateManager timerManager,
        ILogger<PauseTimerCommandHandler> logger)
    {
        _timerManager = timerManager;
        _logger = logger;
    }

    public Task<TimerStatusResponse> Handle(PauseTimerCommand request, CancellationToken cancellationToken)
    {
        var timer = _timerManager.GetTimer(request.SessionId);
        if (timer == null || !string.Equals(timer.Status, "running", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("No running timer found for this session");
        }

        // Calculate remaining time before pausing
        timer.RemainingSeconds = _timerManager.CalculateRemainingSeconds(timer);
        timer.Status = "paused";
        timer.PausedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Timer paused for session {SessionId}: {Remaining}s remaining",
            request.SessionId,
            timer.RemainingSeconds);

        return Task.FromResult(new TimerStatusResponse(
            request.SessionId,
            timer.TimerId,
            timer.Status,
            timer.RemainingSeconds,
            DateTime.UtcNow));
    }
}

public sealed class ResumeTimerCommandHandler : IRequestHandler<ResumeTimerCommand, TimerStatusResponse>
{
    private readonly TimerStateManager _timerManager;
    private readonly ILogger<ResumeTimerCommandHandler> _logger;

    public ResumeTimerCommandHandler(
        TimerStateManager timerManager,
        ILogger<ResumeTimerCommandHandler> logger)
    {
        _timerManager = timerManager;
        _logger = logger;
    }

    public Task<TimerStatusResponse> Handle(ResumeTimerCommand request, CancellationToken cancellationToken)
    {
        var timer = _timerManager.GetTimer(request.SessionId);
        if (timer == null || !string.Equals(timer.Status, "paused", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("No paused timer found for this session");
        }

        timer.Status = "running";
        timer.LastTickAt = DateTime.UtcNow;
        timer.PausedAt = null;

        _logger.LogInformation(
            "Timer resumed for session {SessionId}: {Remaining}s remaining",
            request.SessionId,
            timer.RemainingSeconds);

        return Task.FromResult(new TimerStatusResponse(
            request.SessionId,
            timer.TimerId,
            timer.Status,
            timer.RemainingSeconds,
            DateTime.UtcNow));
    }
}

public sealed class ResetTimerCommandHandler : IRequestHandler<ResetTimerCommand, TimerResetResponse>
{
    private readonly TimerStateManager _timerManager;
    private readonly ILogger<ResetTimerCommandHandler> _logger;

    public ResetTimerCommandHandler(
        TimerStateManager timerManager,
        ILogger<ResetTimerCommandHandler> logger)
    {
        _timerManager = timerManager;
        _logger = logger;
    }

    public Task<TimerResetResponse> Handle(ResetTimerCommand request, CancellationToken cancellationToken)
    {
        _timerManager.RemoveTimer(request.SessionId);

        _logger.LogInformation("Timer reset for session {SessionId}", request.SessionId);

        return Task.FromResult(new TimerResetResponse(
            request.SessionId,
            true,
            DateTime.UtcNow));
    }
}

// ============================================================================
// Coin Flip Handler
// ============================================================================

public sealed class FlipCoinCommandHandler : IRequestHandler<FlipCoinCommand, CoinFlipResponse>
{
    private readonly ILogger<FlipCoinCommandHandler> _logger;

    public FlipCoinCommandHandler(ILogger<FlipCoinCommandHandler> logger)
    {
        _logger = logger;
    }

    public Task<CoinFlipResponse> Handle(FlipCoinCommand request, CancellationToken cancellationToken)
    {
        // Cryptographically secure random for fair coin flip
        var result = RandomNumberGenerator.GetInt32(2) == 0 ? "heads" : "tails";
        var flipId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        _logger.LogInformation(
            "Coin flipped by {ParticipantName} in session {SessionId}: {Result}",
            request.ParticipantName,
            request.SessionId,
            result);

        return Task.FromResult(new CoinFlipResponse(
            flipId,
            request.SessionId,
            request.ParticipantId,
            request.ParticipantName,
            result,
            timestamp));
    }
}

// ============================================================================
// Wheel Spin Handler
// ============================================================================

public sealed class SpinWheelCommandHandler : IRequestHandler<SpinWheelCommand, WheelSpinResponse>
{
    private readonly ILogger<SpinWheelCommandHandler> _logger;

    public SpinWheelCommandHandler(ILogger<SpinWheelCommandHandler> logger)
    {
        _logger = logger;
    }

    public Task<WheelSpinResponse> Handle(SpinWheelCommand request, CancellationToken cancellationToken)
    {
        // Weighted random selection using cryptographically secure random
        var totalWeight = request.Options.Sum(o => o.Weight);
        var randomValue = RandomNumberGenerator.GetInt32(0, (int)(totalWeight * 1000)) / 1000.0;

        var cumulative = 0.0;
        WheelOptionInput selectedOption = request.Options[0];

        foreach (var option in request.Options)
        {
            cumulative += option.Weight;
            if (randomValue <= cumulative)
            {
                selectedOption = option;
                break;
            }
        }

        var spinId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        _logger.LogInformation(
            "Wheel spun by {ParticipantName} in session {SessionId}: selected {Option}",
            request.ParticipantName,
            request.SessionId,
            selectedOption.Label);

        return Task.FromResult(new WheelSpinResponse(
            spinId,
            request.SessionId,
            request.ParticipantId,
            request.ParticipantName,
            selectedOption,
            timestamp));
    }
}

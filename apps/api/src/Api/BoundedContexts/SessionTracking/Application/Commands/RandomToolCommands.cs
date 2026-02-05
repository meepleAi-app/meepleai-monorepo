// Commands for random tools (Issue #3345).

using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

// ============================================================================
// Timer Commands
// ============================================================================

/// <summary>
/// Start a countdown timer for the session.
/// </summary>
public sealed record StartTimerCommand(
    Guid SessionId,
    Guid ParticipantId,
    string ParticipantName,
    int DurationSeconds
) : IRequest<StartTimerResponse>;

/// <summary>
/// Response from starting a timer.
/// </summary>
public sealed record StartTimerResponse(
    Guid SessionId,
    Guid TimerId,
    int DurationSeconds,
    DateTime StartedAt
);

/// <summary>
/// Pause an active timer.
/// </summary>
public sealed record PauseTimerCommand(
    Guid SessionId,
    Guid ParticipantId
) : IRequest<TimerStatusResponse>;

/// <summary>
/// Resume a paused timer.
/// </summary>
public sealed record ResumeTimerCommand(
    Guid SessionId,
    Guid ParticipantId
) : IRequest<TimerStatusResponse>;

/// <summary>
/// Reset a timer.
/// </summary>
public sealed record ResetTimerCommand(
    Guid SessionId,
    Guid ParticipantId
) : IRequest<TimerResetResponse>;

/// <summary>
/// Timer status response.
/// </summary>
public sealed record TimerStatusResponse(
    Guid SessionId,
    Guid TimerId,
    string Status,
    int RemainingSeconds,
    DateTime UpdatedAt
);

/// <summary>
/// Timer reset response.
/// </summary>
public sealed record TimerResetResponse(
    Guid SessionId,
    bool Reset,
    DateTime ResetAt
);

// ============================================================================
// Coin Flip Commands
// ============================================================================

/// <summary>
/// Flip a coin for the session.
/// </summary>
public sealed record FlipCoinCommand(
    Guid SessionId,
    Guid ParticipantId,
    string ParticipantName
) : IRequest<CoinFlipResponse>;

/// <summary>
/// Response from flipping a coin.
/// </summary>
public sealed record CoinFlipResponse(
    Guid FlipId,
    Guid SessionId,
    Guid ParticipantId,
    string ParticipantName,
    string Result,
    DateTime Timestamp
);

// ============================================================================
// Wheel Spin Commands
// ============================================================================

/// <summary>
/// Spin a wheel for the session.
/// </summary>
public sealed record SpinWheelCommand(
    Guid SessionId,
    Guid ParticipantId,
    string ParticipantName,
    List<WheelOptionInput> Options
) : IRequest<WheelSpinResponse>;

/// <summary>
/// Input for wheel option.
/// </summary>
public sealed record WheelOptionInput(
    string Id,
    string Label,
    string Color,
    double Weight
);

/// <summary>
/// Response from spinning a wheel.
/// </summary>
public sealed record WheelSpinResponse(
    Guid SpinId,
    Guid SessionId,
    Guid ParticipantId,
    string ParticipantName,
    WheelOptionInput SelectedOption,
    DateTime Timestamp
);

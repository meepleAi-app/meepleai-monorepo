// SSE events for random tools (Issue #3345).
// These events are broadcast to all session participants.

using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

// ============================================================================
// Timer Events
// ============================================================================

/// <summary>
/// Event when a timer is started.
/// </summary>
public sealed record TimerStartedEvent(
    Guid SessionId,
    Guid TimerId,
    int DurationSeconds,
    Guid StartedBy,
    string StartedByName,
    DateTime StartedAt
) : INotification;

/// <summary>
/// Event when a timer is paused.
/// </summary>
public sealed record TimerPausedEvent(
    Guid SessionId,
    Guid TimerId,
    int RemainingSeconds,
    DateTime PausedAt
) : INotification;

/// <summary>
/// Event when a timer is resumed.
/// </summary>
public sealed record TimerResumedEvent(
    Guid SessionId,
    Guid TimerId,
    int RemainingSeconds,
    DateTime ResumedAt
) : INotification;

/// <summary>
/// Event when a timer completes.
/// </summary>
public sealed record TimerCompletedEvent(
    Guid SessionId,
    Guid TimerId,
    DateTime CompletedAt
) : INotification;

/// <summary>
/// Event when a timer is reset.
/// </summary>
public sealed record TimerResetEvent(
    Guid SessionId,
    Guid TimerId,
    DateTime ResetAt
) : INotification;

// ============================================================================
// Coin Flip Events
// ============================================================================

/// <summary>
/// Event when a coin is flipped.
/// </summary>
public sealed record CoinFlippedEvent(
    Guid SessionId,
    Guid FlipId,
    Guid ParticipantId,
    string ParticipantName,
    string Result,
    DateTime Timestamp
);

// ============================================================================
// Wheel Spin Events
// ============================================================================

/// <summary>
/// Event when a wheel is spun.
/// </summary>
public sealed record WheelSpunEvent(
    Guid SessionId,
    Guid SpinId,
    Guid ParticipantId,
    string ParticipantName,
    WheelOptionDto SelectedOption,
    DateTime Timestamp
);

/// <summary>
/// DTO for wheel option in events.
/// </summary>
public sealed record WheelOptionDto(
    string Id,
    string Label,
    string Color,
    double Weight
);

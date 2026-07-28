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

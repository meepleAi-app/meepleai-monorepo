using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Domain event raised when a session is paused.
/// Placeholder for future pause/resume functionality.
/// </summary>
public record SessionPausedEvent : INotification
{
    /// <summary>
    /// Session unique identifier.
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// User who paused the session.
    /// </summary>
    public required Guid PausedBy { get; init; }

    /// <summary>
    /// When the session was paused.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

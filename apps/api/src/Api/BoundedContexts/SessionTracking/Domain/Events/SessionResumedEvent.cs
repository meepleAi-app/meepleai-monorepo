using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Domain event raised when a paused session is resumed.
/// Placeholder for future pause/resume functionality.
/// </summary>
public record SessionResumedEvent : INotification
{
    /// <summary>
    /// Session unique identifier.
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// User who resumed the session.
    /// </summary>
    public required Guid ResumedBy { get; init; }

    /// <summary>
    /// When the session was resumed.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

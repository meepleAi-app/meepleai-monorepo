using MediatR;

namespace Api.BoundedContexts.Administration.Domain.Events;

/// <summary>
/// Domain event raised when a new activity occurs.
/// Broadcast to all connected dashboard SSE clients.
/// </summary>
public record DashboardActivityEvent : INotification
{
    /// <summary>
    /// Type of activity: game_added, pdf_uploaded, session_started, etc.
    /// </summary>
    public string ActivityType { get; init; } = string.Empty;

    /// <summary>
    /// Activity title or description.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Optional entity ID related to the activity.
    /// </summary>
    public Guid? EntityId { get; init; }

    /// <summary>
    /// Optional user ID who performed the activity.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// When the activity occurred.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

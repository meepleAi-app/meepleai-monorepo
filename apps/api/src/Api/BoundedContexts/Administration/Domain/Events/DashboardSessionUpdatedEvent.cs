using MediatR;

namespace Api.BoundedContexts.Administration.Domain.Events;

/// <summary>
/// Domain event raised when a game session is updated.
/// Broadcast to specific user's dashboard SSE stream.
/// </summary>
public record DashboardSessionUpdatedEvent : INotification
{
    /// <summary>
    /// Session unique identifier.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Session owner user ID.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Game title.
    /// </summary>
    public string GameTitle { get; init; } = string.Empty;

    /// <summary>
    /// Current turn or round number.
    /// </summary>
    public int? Turn { get; init; }

    /// <summary>
    /// Session status: active, paused, completed.
    /// </summary>
    public string Status { get; init; } = "active";

    /// <summary>
    /// When the session was updated.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

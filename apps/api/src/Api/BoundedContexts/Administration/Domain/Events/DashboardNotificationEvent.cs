using MediatR;

namespace Api.BoundedContexts.Administration.Domain.Events;

/// <summary>
/// Domain event raised when a notification is sent.
/// Broadcast to specific user's dashboard SSE stream.
/// </summary>
public record DashboardNotificationEvent : INotification
{
    /// <summary>
    /// Target user ID.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Notification type: info, warning, error, success.
    /// </summary>
    public string Type { get; init; } = "info";

    /// <summary>
    /// Notification title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Notification message body.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Optional link URL for action.
    /// </summary>
    public string? ActionUrl { get; init; }

    /// <summary>
    /// Whether the notification is dismissible.
    /// </summary>
    public bool IsDismissible { get; init; } = true;

    /// <summary>
    /// When the notification was created.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

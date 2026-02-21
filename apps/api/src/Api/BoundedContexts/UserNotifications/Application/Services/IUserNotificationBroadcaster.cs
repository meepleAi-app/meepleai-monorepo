using Api.BoundedContexts.UserNotifications.Application.DTOs;

namespace Api.BoundedContexts.UserNotifications.Application.Services;

/// <summary>
/// In-process broadcaster for real-time user notification delivery via SSE.
/// Issue #5005: Backend SSE stream endpoint /api/v1/notifications/stream.
/// </summary>
internal interface IUserNotificationBroadcaster
{
    /// <summary>
    /// Publishes a notification to all active SSE subscribers for the given user.
    /// Fire-and-forget: silently skips if no subscribers are connected.
    /// </summary>
    void Publish(Guid userId, NotificationDto notification);

    /// <summary>
    /// Opens an async subscription channel for a specific user.
    /// Yields notifications as they arrive. Completes when <paramref name="ct"/> is cancelled.
    /// </summary>
    IAsyncEnumerable<NotificationDto> SubscribeAsync(Guid userId, CancellationToken ct);
}

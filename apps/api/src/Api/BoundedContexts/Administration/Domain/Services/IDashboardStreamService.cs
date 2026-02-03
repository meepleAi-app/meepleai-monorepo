using MediatR;

namespace Api.BoundedContexts.Administration.Domain.Services;

/// <summary>
/// Service for real-time dashboard updates using Server-Sent Events (SSE).
/// Manages global event subscriptions and broadcasting for dashboard widgets.
/// Unlike session-scoped ISessionSyncService, this is a global pub/sub for all dashboard updates.
/// </summary>
public interface IDashboardStreamService
{
    /// <summary>
    /// Subscribes to real-time dashboard events.
    /// Returns an async stream of events for SSE broadcasting to authenticated users.
    /// </summary>
    /// <param name="userId">User ID for filtering user-specific events.</param>
    /// <param name="ct">Cancellation token for cleanup on client disconnect.</param>
    /// <returns>Async enumerable stream of dashboard events.</returns>
    IAsyncEnumerable<INotification> SubscribeToDashboardEvents(
        Guid userId,
        CancellationToken ct);

    /// <summary>
    /// Publishes an event to all dashboard subscribers.
    /// Events are broadcasted in real-time to connected SSE clients.
    /// </summary>
    /// <param name="evt">Event to broadcast.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PublishEventAsync(INotification evt, CancellationToken ct);

    /// <summary>
    /// Publishes an event to a specific user's dashboard stream.
    /// </summary>
    /// <param name="userId">Target user ID.</param>
    /// <param name="evt">Event to broadcast.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PublishEventToUserAsync(Guid userId, INotification evt, CancellationToken ct);
}

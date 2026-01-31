using Api.BoundedContexts.SessionTracking.Domain.Events;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Services;

/// <summary>
/// Service for real-time session synchronization using Server-Sent Events (SSE).
/// Manages event subscriptions and broadcasting for collaborative game sessions.
/// </summary>
public interface ISessionSyncService
{
    /// <summary>
    /// Subscribes to real-time events for a specific session.
    /// Returns an async stream of events for SSE broadcasting.
    /// </summary>
    /// <param name="sessionId">Session to subscribe to.</param>
    /// <param name="ct">Cancellation token for cleanup on client disconnect.</param>
    /// <returns>Async enumerable stream of session events.</returns>
    IAsyncEnumerable<INotification> SubscribeToSessionEvents(
        Guid sessionId,
        CancellationToken ct);

    /// <summary>
    /// Publishes an event to all subscribers of a session.
    /// Events are broadcasted in real-time to connected SSE clients.
    /// </summary>
    /// <param name="sessionId">Session to publish to.</param>
    /// <param name="evt">Event to broadcast.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PublishEventAsync(Guid sessionId, INotification evt, CancellationToken ct);
}

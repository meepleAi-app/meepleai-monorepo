using System.Runtime.InteropServices;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Services;

/// <summary>
/// Enhanced broadcasting service for real-time session state delivery via SSE.
/// Supports Redis Pub/Sub for multi-instance, connection pool limits, event buffering,
/// selective broadcasting, and Last-Event-ID reconnection.
/// Issue #4764 - SSE Streaming Infrastructure + Session State Broadcasting
/// </summary>
public interface ISessionBroadcastService
{
    /// <summary>
    /// Subscribes to real-time events for a specific session with enhanced features.
    /// Supports reconnection via lastEventId and per-user selective filtering.
    /// </summary>
    /// <param name="sessionId">Session to subscribe to.</param>
    /// <param name="userId">Subscribing user ID for selective broadcasting.</param>
    /// <param name="lastEventId">Last received event ID for reconnection (null for fresh connect).</param>
    /// <param name="ct">Cancellation token for cleanup on client disconnect.</param>
    /// <returns>Async enumerable stream of SSE event envelopes.</returns>
    IAsyncEnumerable<SseEventEnvelope> SubscribeAsync(
        Guid sessionId,
        Guid userId,
        string? lastEventId,
        CancellationToken ct);

    /// <summary>
    /// Publishes an event to all subscribers of a session.
    /// Handles event buffering, Redis Pub/Sub distribution, and selective filtering.
    /// </summary>
    /// <param name="sessionId">Session to publish to.</param>
    /// <param name="evt">Domain event to broadcast.</param>
    /// <param name="visibility">Who can see this event.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PublishAsync(
        Guid sessionId,
        INotification evt,
        EventVisibility visibility = default,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the number of active connections for a session.
    /// </summary>
    int GetConnectionCount(Guid sessionId);

    /// <summary>
    /// Disconnects all subscribers from a session (used on session end).
    /// </summary>
    Task DisconnectAllAsync(Guid sessionId, CancellationToken ct = default);
}

/// <summary>
/// SSE event envelope with metadata for reconnection and typed event names.
/// </summary>
public record SseEventEnvelope
{
    /// <summary>Unique event ID for idempotent delivery and Last-Event-ID reconnection.</summary>
    public required string Id { get; init; }

    /// <summary>Typed SSE event name (e.g., "session:score", "session:turn").</summary>
    public required string EventType { get; init; }

    /// <summary>Serialized event data (JSON).</summary>
    public required object Data { get; init; }

    /// <summary>Timestamp of event creation.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Controls who receives a broadcast event.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public record struct EventVisibility
{
    /// <summary>If null, event is visible to all subscribers. If set, only this user sees it.</summary>
    public Guid? TargetUserId { get; init; }

    /// <summary>If true, event is visible to all (default). If false, only TargetUserId sees it.</summary>
    public bool IsPublic { get; init; }

    /// <summary>Creates a public event visible to all subscribers.</summary>
    public static EventVisibility Public => new() { IsPublic = true };

    /// <summary>Creates a private event visible only to the specified user.</summary>
    public static EventVisibility PrivateTo(Guid userId) => new() { TargetUserId = userId, IsPublic = false };
}

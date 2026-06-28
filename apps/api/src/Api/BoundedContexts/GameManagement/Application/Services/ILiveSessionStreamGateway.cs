namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Canonical event DTO for the live-session SSE stream.
/// Deliberately neutral: no references to SessionTracking or KnowledgeBase types (ADR-083 ACL).
/// </summary>
/// <param name="Type">Event type discriminator, e.g. "session:score", "session:turn".</param>
/// <param name="Data">Opaque event payload — consumers cast or deserialise as needed.</param>
/// <param name="Id">
/// SSE event id, populated on the subscribe path so the endpoint (Task 4) can emit
/// <c>id:</c> for Last-Event-ID resume. <c>null</c> on the broadcast path — the
/// underlying broadcast service assigns a monotonic id internally.
/// </param>
public record LiveSessionStreamEvent(string Type, object Data, string? Id = null);

/// <summary>
/// Anti-corruption layer (ACL) for the live-session SSE stream gateway.
/// GameManagement Application layer uses this interface without importing any
/// SessionTracking or KnowledgeBase types (ADR-083 SP2).
/// Implementations live in Infrastructure and may use Redis Pub/Sub, SignalR hubs, etc.
/// </summary>
public interface ILiveSessionStreamGateway
{
    /// <summary>
    /// Returns an async stream of events for the given live session.
    /// The stream ends when the session is closed or the <paramref name="ct"/> is cancelled.
    /// </summary>
    /// <param name="liveSessionId">Identifier of the live session to subscribe to.</param>
    /// <param name="userId">Identifier of the subscribing user (for authorization / fan-out filtering).</param>
    /// <param name="lastEventId">Last SSE event-id received by the client (for resumption), or <c>null</c>.</param>
    /// <param name="ct">Cancellation token; triggers clean stream termination.</param>
    IAsyncEnumerable<LiveSessionStreamEvent> SubscribeAsync(
        Guid liveSessionId,
        Guid userId,
        string? lastEventId,
        CancellationToken ct);

    /// <summary>
    /// Broadcasts an event to all subscribers of the given live session.
    /// </summary>
    /// <param name="liveSessionId">Identifier of the target live session.</param>
    /// <param name="evt">Event to broadcast.</param>
    /// <param name="ct">Optional cancellation token.</param>
    Task BroadcastAsync(Guid liveSessionId, LiveSessionStreamEvent evt, CancellationToken ct = default);
}

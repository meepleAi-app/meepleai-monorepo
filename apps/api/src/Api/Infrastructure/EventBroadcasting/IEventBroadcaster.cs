using Api.BoundedContexts.Administration.Application.Queries.AdminEvents;

namespace Api.Infrastructure.EventBroadcasting;

/// <summary>
/// In-process pub/sub bus for broadcasting <see cref="DomainEventDto"/> events to
/// Server-Sent Event (SSE) subscribers in the Admin Monitor LiveEventLog (F4.1, issue #1718).
///
/// Lifecycle:
///   - Registered as a singleton in DI (see <c>EventBroadcastingExtensions</c>).
///   - <see cref="Publish"/> is called by <c>DomainEventBroadcastInterceptor</c> after
///     each successful <c>SaveChangesAsync</c> commit.
///   - <see cref="Subscribe"/> is called once per SSE connection and yields events until
///     the caller cancels or the broadcaster is disposed.
/// </summary>
internal interface IEventBroadcaster
{
    /// <summary>
    /// Publishes <paramref name="evt"/> to all active subscribers whose
    /// <see cref="EventBroadcastFilter"/> matches the event.
    /// Non-blocking; never throws.
    /// </summary>
    /// <param name="evt">The domain event DTO to broadcast.</param>
    void Publish(DomainEventDto evt);

    /// <summary>
    /// Returns an async stream of domain events that match <paramref name="filter"/>.
    /// The stream continues until <paramref name="ct"/> is cancelled or the broadcaster
    /// is disposed (at which point the <c>await foreach</c> loop exits normally).
    /// </summary>
    /// <param name="filter">
    /// Per-subscriber filter. Pass <c>new EventBroadcastFilter()</c> (all nulls) for
    /// no filtering (receives every event).
    /// </param>
    /// <param name="ct">
    /// Token tied to the SSE connection lifetime. Cancel to unsubscribe and release
    /// the internal channel slot.
    /// </param>
    IAsyncEnumerable<DomainEventDto> Subscribe(EventBroadcastFilter filter, CancellationToken ct);
}

using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.AdminEvents;

/// <summary>
/// Streaming query for the Admin Monitor SSE endpoint (F4.1 issue #1718).
///
/// <para>
/// Yields <see cref="DomainEventDto"/> events via MediatR's
/// <c>IStreamRequest&lt;TResponse&gt;</c> / <c>IMediator.CreateStream</c> mechanism.
/// </para>
///
/// <para>
/// <b>Stream lifecycle:</b>
/// <list type="number">
///   <item>Subscribe EAGERLY to <see cref="Api.Infrastructure.EventBroadcasting.IEventBroadcaster"/>
///         (prevents events lost during backfill).</item>
///   <item>If <see cref="LastEventId"/> is non-null, dispatch an internal
///         <see cref="GetAdminEventsQuery"/> (Limit=200) and yield backfill events oldest-first,
///         tracking their IDs to avoid duplicates.</item>
///   <item>Forward broadcaster events indefinitely, skipping already-seen IDs, until the
///         <c>CancellationToken</c> is cancelled (client disconnect).</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Heartbeat and SSE framing stay in the endpoint</b> — this query yields only
/// <see cref="DomainEventDto"/> items; transport-level concerns (headers, ":ok\n\n", ":hb\n\n",
/// "id: ...\ndata: ...\n\n") are handled by <c>HandleGetEventsStream</c>.
/// </para>
/// </summary>
/// <param name="LastEventId">
/// W3C SSE <c>Last-Event-ID</c> header value from the reconnecting client.
/// When set, triggers DB backfill of events newer than this cursor.
/// </param>
/// <param name="EventTypes">
/// Optional allow-list of <c>EventType</c> aliases (e.g. "agent.created").
/// Null or empty = no filter.
/// </param>
/// <param name="AggregateTypes">
/// Optional allow-list of <c>AggregateType</c> names (e.g. "Agent").
/// Null or empty = no filter.
/// </param>
/// <param name="UserId">Optional equality filter on <c>UserId</c>.</param>
/// <param name="AggregateId">Optional equality filter on <c>AggregateId</c>.</param>
internal sealed record GetAdminEventsStreamQuery(
    Guid? LastEventId = null,
    IReadOnlyList<string>? EventTypes = null,
    IReadOnlyList<string>? AggregateTypes = null,
    Guid? UserId = null,
    Guid? AggregateId = null
) : IStreamingQuery<DomainEventDto>;

using System.Runtime.CompilerServices;
using Api.Infrastructure.EventBroadcasting;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.AdminEvents;

/// <summary>
/// Handles <see cref="GetAdminEventsStreamQuery"/>: streams <see cref="DomainEventDto"/>
/// events via MediatR's <c>IStreamRequest</c> mechanism.
///
/// <para>
/// <b>Race-free subscription pattern</b> — the channel is registered in
/// <see cref="IEventBroadcaster.Subscribe"/> <i>before</i> the backfill DB query runs,
/// so any events published during the backfill window are buffered and forwarded after
/// backfill completes without any gap.
/// </para>
///
/// <para>
/// <b>Backfill logic (when <c>LastEventId</c> is set):</b><br/>
/// Fetches the 200 most recent events via <see cref="GetAdminEventsQuery"/>, finds the
/// cursor position of <c>LastEventId</c> using <c>TakeWhile</c>, reverses the slice to
/// yield oldest-first, and tracks yielded IDs in <c>seenIds</c> for dedup.
/// </para>
///
/// <para>
/// <b>Note on DI</b>: per CQRS pattern, handlers (unlike endpoints) MAY inject
/// infrastructure services directly.  <see cref="IEventBroadcaster"/> and
/// <see cref="IMediator"/> are both valid handler-level dependencies.
/// </para>
///
/// F4.1 issue #1718.
/// </summary>
internal sealed class GetAdminEventsStreamQueryHandler
    : IStreamingQueryHandler<GetAdminEventsStreamQuery, DomainEventDto>
{
    private const int BackfillLimit = 200;

    private readonly IEventBroadcaster _broadcaster;
    private readonly IMediator _mediator;

    public GetAdminEventsStreamQueryHandler(
        IEventBroadcaster broadcaster,
        IMediator mediator)
    {
        _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    // S4456: iterator method separated from parameter-validating outer method per Sonar convention.
    // The outer method validates, subscribes eagerly, and delegates to the iterator.
#pragma warning disable S4456
    public async IAsyncEnumerable<DomainEventDto> Handle(
        GetAdminEventsStreamQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
#pragma warning restore S4456
    {
        ArgumentNullException.ThrowIfNull(request);

        // ── Subscribe EAGERLY to broadcaster before any DB query ──
        // Events published after this line but before backfill completes are buffered
        // in the channel and will be forwarded in the live-stream phase below.
        var filter = new EventBroadcastFilter(
            EventTypes: request.EventTypes,
            AggregateTypes: request.AggregateTypes,
            UserId: request.UserId,
            AggregateId: request.AggregateId);

        var liveStream = _broadcaster.Subscribe(filter, cancellationToken);

        // ── Backfill: yield events newer than LastEventId, oldest-first ──
        var seenIds = new HashSet<Guid>();

        if (request.LastEventId.HasValue)
        {
            var lastGuid = request.LastEventId.Value;

            var backfillQuery = new GetAdminEventsQuery(
                Since: null,
                Limit: BackfillLimit,
                EventTypes: request.EventTypes,
                AggregateTypes: request.AggregateTypes,
                UserId: request.UserId,
                AggregateId: request.AggregateId);

            var backfill = await _mediator.Send(backfillQuery, cancellationToken)
                .ConfigureAwait(false);

            // Events are ordered DESC — take everything before the cursor (i.e. newer),
            // then reverse to yield oldest-first.
            var backfillEvents = backfill.Events
                .TakeWhile(e => e.Id != lastGuid)
                .Reverse()
                .ToList();

            foreach (var evt in backfillEvents)
            {
                if (cancellationToken.IsCancellationRequested) yield break;
                seenIds.Add(evt.Id);
                yield return evt;
            }
        }

        // ── Live-stream phase: forward broadcaster events with dedup ──
        await foreach (var evt in liveStream.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (seenIds.Contains(evt.Id)) continue;
            yield return evt;
        }
    }
}

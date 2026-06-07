using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.DomainEventOutbox;

/// <summary>
/// Handler for <see cref="GetFailedEventOutboxRowsQuery"/>. Orders by
/// <c>EnqueuedAt</c> DESC — the partial index
/// <c>ix_domain_event_outbox_failed_recent</c> is built on the same column for
/// O(limit) lookups.
///
/// <para><b>Caveat: ordering is by enqueue time, NOT by the moment the row
/// transitioned to Failed.</b> <see cref="DomainEventOutboxEntity.MarkFailed"/>
/// does not update <c>EnqueuedAt</c>. So a row enqueued 6h ago that retried for
/// hours and failed terminally 30s ago will appear BEHIND a row enqueued 5min
/// ago that failed on first attempt. Operators investigating a poison-message
/// spike should cross-reference <c>Attempts</c> + <c>LastError</c> to identify
/// the actual recent failures. A future <c>FailedAt</c> column is tracked as
/// follow-up if this becomes a real triage blocker.</para>
///
/// Issue #1535 T6.
/// </summary>
internal sealed class GetFailedEventOutboxRowsQueryHandler
    : IQueryHandler<GetFailedEventOutboxRowsQuery, IReadOnlyList<DomainEventOutboxRowDto>>
{
    private const int MinLimit = 1;
    private const int MaxLimit = 200;

    private readonly MeepleAiDbContext _db;

    public GetFailedEventOutboxRowsQueryHandler(MeepleAiDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<DomainEventOutboxRowDto>> Handle(
        GetFailedEventOutboxRowsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var limit = Math.Clamp(request.Limit, MinLimit, MaxLimit);

        var rows = await _db.DomainEventOutbox
            .AsNoTracking()
            .Where(r => r.Status == DomainEventOutboxStatus.Failed)
            .OrderByDescending(r => r.EnqueuedAt)
            .Take(limit)
            .Select(r => new DomainEventOutboxRowDto(
                r.Id,
                r.EventType,
                r.Status,
                r.Attempts,
                r.LastError,
                r.OccurredAt,
                r.EnqueuedAt,
                r.DispatchedAt,
                r.NextAttemptAt,
                r.CorrelationId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows;
    }
}

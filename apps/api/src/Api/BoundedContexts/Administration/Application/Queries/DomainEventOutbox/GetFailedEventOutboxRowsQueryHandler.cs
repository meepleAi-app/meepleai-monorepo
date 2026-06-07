using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.DomainEventOutbox;

/// <summary>
/// Handler for <see cref="GetFailedEventOutboxRowsQuery"/>. Orders by
/// <c>FailedAt</c> DESC — the partial index <c>ix_domain_event_outbox_failed_recent</c>
/// is built on the same column for O(limit) lookups. Operators investigating a
/// poison-message spike see the most-recently-failed rows first, regardless of
/// when those rows were originally enqueued.
///
/// <para>Issue #1535 T6 + follow-up: switched from <c>EnqueuedAt</c> DESC (which
/// buried recent failures behind older retry timeouts) to <c>FailedAt</c> DESC. The
/// <c>FailedAt</c> column is set by <see cref="DomainEventOutboxEntity.MarkFailed"/>
/// and cleared by <see cref="DomainEventOutboxEntity.RearmFromFailed"/>.</para>
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
            .OrderByDescending(r => r.FailedAt)
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
                r.FailedAt,
                r.CorrelationId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows;
    }
}

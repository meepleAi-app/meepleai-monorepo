using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.DomainEventOutbox;

/// <summary>
/// Handler for <see cref="GetPendingEventOutboxRowsQuery"/>. Orders by
/// <c>EnqueuedAt</c> ASC (FIFO — same order the processor drains in) so the
/// admin panel mirrors the actual queue head.
///
/// Issue #1535 T6.
/// </summary>
internal sealed class GetPendingEventOutboxRowsQueryHandler
    : IQueryHandler<GetPendingEventOutboxRowsQuery, IReadOnlyList<DomainEventOutboxRowDto>>
{
    private const int MinLimit = 1;
    private const int MaxLimit = 200;

    private readonly MeepleAiDbContext _db;

    public GetPendingEventOutboxRowsQueryHandler(MeepleAiDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<DomainEventOutboxRowDto>> Handle(
        GetPendingEventOutboxRowsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var limit = Math.Clamp(request.Limit, MinLimit, MaxLimit);

        var rows = await _db.DomainEventOutbox
            .AsNoTracking()
            .Where(r => r.Status == DomainEventOutboxStatus.Pending)
            .OrderBy(r => r.EnqueuedAt)
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

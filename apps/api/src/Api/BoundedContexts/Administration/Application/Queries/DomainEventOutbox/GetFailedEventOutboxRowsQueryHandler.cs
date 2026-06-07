using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.DomainEventOutbox;

/// <summary>
/// Handler for <see cref="GetFailedEventOutboxRowsQuery"/>. Orders by
/// <c>EnqueuedAt</c> DESC (most-recent failures first) so an operator who opens
/// the admin panel after a poison-message spike sees the newest rows at the top.
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

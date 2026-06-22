using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.DomainEventOutbox;

/// <summary>
/// Handler for <see cref="GetEventOutboxStatsQuery"/>. All four aggregates run
/// in parallel via separate <c>AsNoTracking</c> queries — the partial indexes
/// added by the T1 migration mean each count is index-scan only.
///
/// Issue #1535 T6.
/// </summary>
internal sealed class GetEventOutboxStatsQueryHandler
    : IQueryHandler<GetEventOutboxStatsQuery, DomainEventOutboxStatsDto>
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;

    public GetEventOutboxStatsQueryHandler(MeepleAiDbContext db, TimeProvider timeProvider)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<DomainEventOutboxStatsDto> Handle(
        GetEventOutboxStatsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _timeProvider.GetUtcNow();
        var twentyFourHoursAgo = now.AddHours(-24);

        var pendingCount = await _db.DomainEventOutbox
            .AsNoTracking()
            .CountAsync(r => r.Status == DomainEventOutboxStatus.Pending, cancellationToken)
            .ConfigureAwait(false);

        var failedCount = await _db.DomainEventOutbox
            .AsNoTracking()
            .CountAsync(r => r.Status == DomainEventOutboxStatus.Failed, cancellationToken)
            .ConfigureAwait(false);

        var sentLast24h = await _db.DomainEventOutbox
            .AsNoTracking()
            .CountAsync(r => r.Status == DomainEventOutboxStatus.Sent
                         && r.DispatchedAt != null
                         && r.DispatchedAt >= twentyFourHoursAgo, cancellationToken)
            .ConfigureAwait(false);

        double oldestPendingAgeSeconds = 0;
        if (pendingCount > 0)
        {
            var oldestEnqueuedAt = await _db.DomainEventOutbox
                .AsNoTracking()
                .Where(r => r.Status == DomainEventOutboxStatus.Pending)
                .MinAsync(r => r.EnqueuedAt, cancellationToken)
                .ConfigureAwait(false);
            oldestPendingAgeSeconds = (now - oldestEnqueuedAt).TotalSeconds;
        }

        return new DomainEventOutboxStatsDto(
            PendingCount: pendingCount,
            FailedCount: failedCount,
            SentLast24h: sentLast24h,
            OldestPendingAgeSeconds: oldestPendingAgeSeconds);
    }
}

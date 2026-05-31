using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.AdminEvents;

/// <summary>
/// Handles <see cref="GetAdminEventsQuery"/>: reads domain events from
/// <c>domain_event_logs</c> with admin-scope (cross-user) and optional filters.
///
/// Filtering rules applied in order:
/// <list type="number">
///   <item>Retention: <c>LoggedAt &gt;= UtcNow - 90 days</c> (always applied)</item>
///   <item>Since cursor: <c>LoggedAt &lt; Since</c> (if <c>Since</c> is set)</item>
///   <item>EventTypes: <c>EventType IN (...)</c> (if non-empty)</item>
///   <item>AggregateTypes: <c>AggregateType IN (...)</c> (if non-empty)</item>
///   <item>UserId equality (if <c>UserId</c> is set)</item>
///   <item>AggregateId equality (if <c>AggregateId</c> is set)</item>
/// </list>
///
/// Results are ordered by <c>LoggedAt DESC</c> and limited to
/// <c>Math.Clamp(Limit, 1, 1000)</c> rows.
///
/// F4.1 issue #1718 — Task 1.1.
/// </summary>
internal sealed class GetAdminEventsQueryHandler
    : IQueryHandler<GetAdminEventsQuery, GetAdminEventsResult>
{
    private const int RetentionDays = 90;
    private const int MinLimit = 1;
    private const int MaxLimit = 1000;

    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;

    public GetAdminEventsQueryHandler(MeepleAiDbContext db, TimeProvider timeProvider)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<GetAdminEventsResult> Handle(
        GetAdminEventsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var retentionCutoff = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-RetentionDays);

        var query = _db.DomainEventLogs
            .AsNoTracking()
            .Where(e => e.LoggedAt >= retentionCutoff);

        if (request.Since.HasValue)
        {
            query = query.Where(e => e.LoggedAt < request.Since.Value);
        }

        if (request.EventTypes is { Count: > 0 })
        {
            query = query.Where(e => request.EventTypes.Contains(e.EventType));
        }

        if (request.AggregateTypes is { Count: > 0 })
        {
            query = query.Where(e => e.AggregateType != null
                                     && request.AggregateTypes.Contains(e.AggregateType));
        }

        if (request.UserId.HasValue)
        {
            query = query.Where(e => e.UserId == request.UserId.Value);
        }

        if (request.AggregateId.HasValue)
        {
            query = query.Where(e => e.AggregateId == request.AggregateId.Value);
        }

        var limit = Math.Clamp(request.Limit, MinLimit, MaxLimit);

        var rows = await query
            .OrderByDescending(e => e.LoggedAt)
            .Take(limit)
            .Select(e => new DomainEventDto(
                e.Id,
                e.EventId,
                e.EventType,
                e.AggregateType,
                e.AggregateId,
                e.UserId,
                e.PayloadJson,
                e.PayloadVersion,
                e.OccurredAt,
                e.LoggedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new GetAdminEventsResult(rows);
    }
}

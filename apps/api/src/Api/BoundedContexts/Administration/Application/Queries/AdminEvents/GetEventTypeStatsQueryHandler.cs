using Api.Infrastructure;
using Api.Infrastructure.DomainEventLog;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.AdminEvents;

/// <summary>
/// Handles <see cref="GetEventTypeStatsQuery"/>: groups domain events from the
/// last 24 hours by <c>EventType</c> and merges the result with the full set of
/// aliases registered in <see cref="EventTypeRegistry"/> so that every known
/// event type is always present in the output — even when it has no recent activity.
///
/// Merge strategy:
/// <list type="bullet">
///   <item>For each alias in <see cref="EventTypeRegistry.AliasByType"/>, look up
///     the DB-side aggregate (COUNT + MAX) for the 24-hour window.</item>
///   <item>If no DB row exists for an alias, emit <c>Count = 0</c>, <c>LastSeenAt = null</c>.</item>
/// </list>
///
/// Output is ordered alphabetically by <c>EventType</c> for stable API responses.
///
/// F4.1 issue #1718 — Task 1.2.
/// </summary>
internal sealed class GetEventTypeStatsQueryHandler
    : IQueryHandler<GetEventTypeStatsQuery, GetEventTypeStatsResult>
{
    private const int WindowDays = 1;

    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;

    public GetEventTypeStatsQueryHandler(MeepleAiDbContext db, TimeProvider timeProvider)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<GetEventTypeStatsResult> Handle(
        GetEventTypeStatsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var windowStart = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-WindowDays);

        // GROUP BY EventType — count + latest timestamp per type within the 24h window
        var dbStats = await _db.DomainEventLogs
            .AsNoTracking()
            .Where(e => e.LoggedAt >= windowStart)
            .GroupBy(e => e.EventType)
            .Select(g => new
            {
                EventType = g.Key,
                Count = g.Count(),
                LastSeenAt = g.Max(e => e.LoggedAt)
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Index by alias for O(1) lookup during merge.
        // Ordinal comparison matches how EventType strings are written in the registry.
        var statsByType = dbStats.ToDictionary(
            s => s.EventType,
            s => s,
            StringComparer.Ordinal);

        // Merge with all registered aliases — types absent from DB appear with Count=0
        var knownAliases = EventTypeRegistry.AliasByType.Values;

        var merged = knownAliases
            .Select(alias =>
            {
                if (statsByType.TryGetValue(alias, out var stat))
                {
                    return new EventTypeStat(alias, stat.Count, stat.LastSeenAt);
                }

                return new EventTypeStat(alias, 0, null);
            })
            .OrderBy(s => s.EventType, StringComparer.Ordinal)
            .ToList();

        return new GetEventTypeStatsResult(merged);
    }
}

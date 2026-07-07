using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Counts diary events per session for a game night — Issue #2721. A single GROUP BY over the
/// SessionTracking session-events (cross-BC read, scoped by game-night id, soft-delete aware).
/// </summary>
internal static class GameNightSummaryEventCounts
{
    public static async Task<IReadOnlyDictionary<Guid, int>> BuildAsync(
        MeepleAiDbContext db, Guid gameNightId, CancellationToken cancellationToken)
    {
        var counts = await db.SessionEvents
            .AsNoTracking()
            .Where(e => e.GameNightId == gameNightId && !e.IsDeleted)
            .GroupBy(e => e.SessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count, cancellationToken)
            .ConfigureAwait(false);

        return counts;
    }
}

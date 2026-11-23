using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetBadgeLeaderboard;

/// <summary>
/// #5136: the rank of each contributor of the previous calendar month (UTC, closed), by number of contributions.
/// The leaderboard badges (requirement TopContributor) are awarded on it: a rank within the badge's TopContributorRank.
/// </summary>
/// <remarks>
/// A competition rank (1, 1, 3, …): contributors with the same count share a rank, so a tie never depends on the order
/// in which users are recalculated. The badge leaderboard (GetBadgeLeaderboardQuery) breaks ties on the badges held,
/// leaderboard badges included, so it cannot decide who earns them (review of #5137).
/// </remarks>
internal sealed record GetPreviousMonthContributionRanksQuery : IQuery<IReadOnlyDictionary<Guid, int>>;

internal sealed class GetPreviousMonthContributionRanksQueryHandler
    : IQueryHandler<GetPreviousMonthContributionRanksQuery, IReadOnlyDictionary<Guid, int>>
{
    private readonly MeepleAiDbContext _context;
    private readonly TimeProvider _timeProvider;

    public GetPreviousMonthContributionRanksQueryHandler(MeepleAiDbContext context, TimeProvider timeProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> Handle(
        GetPreviousMonthContributionRanksQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var from = monthStart.AddMonths(-1);

        var counts = await _context.Set<ContributionRecordEntity>()
            .AsNoTracking()
            .Where(cr => cr.ContributedAt >= from && cr.ContributedAt < monthStart)
            .GroupBy(cr => cr.Contributor.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Competition rank: 1 + the number of contributors with strictly more contributions.
        return counts.ToDictionary(
            c => c.UserId,
            c => 1 + counts.Count(other => other.Count > c.Count));
    }
}

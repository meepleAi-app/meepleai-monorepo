using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Builds a per-session top-N leaderboard (by <c>FinalRank</c>) from the cross-BC SessionTracking
/// participants — Issue #2722. Rank is universal across the polymorphic scoring types
/// (Points/BinaryWin/Objectives/Ranking), so a numeric score is intentionally omitted here.
/// </summary>
internal static class GameNightSummaryLeaderboard
{
    private const int TopN = 3;

    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<GameNightRecapPlayerDto>>> BuildAsync(
        MeepleAiDbContext db, GameNightEvent night, CancellationToken cancellationToken)
    {
        var sessionIds = night.Sessions.Select(s => s.SessionId).ToList();
        if (sessionIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<GameNightRecapPlayerDto>>();

        var participants = await db.SessionTrackingParticipants
            .AsNoTracking()
            .Where(p => sessionIds.Contains(p.SessionId))
            .Select(p => new { p.SessionId, p.DisplayName, p.FinalRank })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return participants
            .GroupBy(p => p.SessionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<GameNightRecapPlayerDto>)group
                    .OrderBy(p => p.FinalRank ?? int.MaxValue)
                    .Take(TopN)
                    .Select(p => new GameNightRecapPlayerDto(p.DisplayName, p.FinalRank))
                    .ToList());
    }
}

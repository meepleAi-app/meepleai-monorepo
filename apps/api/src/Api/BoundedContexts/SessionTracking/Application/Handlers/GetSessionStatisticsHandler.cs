using System.Globalization;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

public class GetSessionStatisticsHandler : IRequestHandler<GetSessionStatisticsQuery, SessionStatisticsDto>
{
    private readonly MeepleAiDbContext _context;

    public GetSessionStatisticsHandler(MeepleAiDbContext context)
    {
        _context = context;
    }

    public async Task<SessionStatisticsDto> Handle(GetSessionStatisticsQuery request, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-request.MonthsBack);

        var sessions = await _context.SessionTrackingSessions
            .Where(s => s.CreatedBy == request.UserId && !s.IsDeleted && s.SessionDate >= cutoff)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalSessions = sessions.Count;
        var totalGamesPlayed = sessions
            .Where(s => s.GameId.HasValue)
            .Select(s => s.GameId)
            .Distinct()
            .Count();

        var avgDuration = totalSessions > 0 && sessions.Any(s => s.FinalizedAt.HasValue)
            ? TimeSpan.FromTicks((long)sessions
                .Where(s => s.FinalizedAt.HasValue)
                .Average(s => (s.FinalizedAt!.Value - s.SessionDate).Ticks))
            : TimeSpan.Zero;

        var mostPlayed = sessions
            .Where(s => s.GameId.HasValue)
            .GroupBy(s => s.GameId!.Value)
            .Select(g => new GamePlayFrequencyDto(
                g.Key,
                string.Concat("Game-", g.Key.ToString("N", CultureInfo.InvariantCulture).AsSpan(0, 8)),
                g.Count()))
            .OrderByDescending(g => g.PlayCount)
            .Take(10)
            .ToList();

        var sessionIds = sessions.Select(s => s.Id).ToHashSet();

        var recentScoreEntries = await _context.SessionTrackingScoreEntries
            .Where(se => sessionIds.Contains(se.SessionId))
            .OrderByDescending(se => se.Timestamp)
            .Take(20)
            .Select(se => new { se.Timestamp, se.SessionId, se.ScoreValue })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var recentScores = recentScoreEntries
            .Select(se => new ScoreTrendDto(
                se.Timestamp,
                string.Concat("Game-", se.SessionId.ToString("N", CultureInfo.InvariantCulture).AsSpan(0, 8)),
                se.ScoreValue))
            .ToList();

        var monthlyActivity = sessions
            .GroupBy(s => s.SessionDate.ToString("yyyy-MM", CultureInfo.InvariantCulture), StringComparer.Ordinal)
            .Select(g => new MonthlyPlayCountDto(g.Key, g.Count()))
            .OrderBy(m => m.Month, StringComparer.Ordinal)
            .ToList();

        return new SessionStatisticsDto(
            totalSessions,
            totalGamesPlayed,
            avgDuration.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture),
            mostPlayed,
            recentScores,
            monthlyActivity);
    }
}

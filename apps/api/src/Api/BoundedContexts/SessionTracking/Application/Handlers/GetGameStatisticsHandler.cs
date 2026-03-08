using System.Globalization;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

public class GetGameStatisticsHandler : IRequestHandler<GetGameStatisticsQuery, GameStatisticsDto>
{
    private readonly MeepleAiDbContext _context;

    public GetGameStatisticsHandler(MeepleAiDbContext context)
    {
        _context = context;
    }

    public async Task<GameStatisticsDto> Handle(GetGameStatisticsQuery request, CancellationToken cancellationToken)
    {
        var sessions = await _context.SessionTrackingSessions
            .Where(s => s.CreatedBy == request.UserId && s.GameId == request.GameId && !s.IsDeleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalPlays = sessions.Count;

        var sessionIds = sessions.Select(s => s.Id).ToHashSet();

        var scores = await _context.SessionTrackingScoreEntries
            .Where(se => sessionIds.Contains(se.SessionId))
            .OrderByDescending(se => se.Timestamp)
            .Take(50)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var wins = scores.Count(s => string.Equals(s.Category, "winner", StringComparison.Ordinal));
        var winRate = totalPlays > 0 ? (double)wins / totalPlays : 0.0;
        var avgScore = scores.Count > 0 ? scores.Average(s => (double)s.ScoreValue) : 0.0;
        var highScore = scores.Count > 0 ? scores.Max(s => s.ScoreValue) : 0m;

        var avgDuration = totalPlays > 0 && sessions.Any(s => s.FinalizedAt.HasValue)
            ? TimeSpan.FromTicks((long)sessions
                .Where(s => s.FinalizedAt.HasValue)
                .Average(s => (s.FinalizedAt!.Value - s.SessionDate).Ticks))
            : TimeSpan.Zero;

        var gameName = string.Concat("Game-", request.GameId.ToString("N", CultureInfo.InvariantCulture).AsSpan(0, 8));

        var scoreHistory = scores
            .Select(se => new ScoreTrendDto(se.Timestamp, gameName, se.ScoreValue))
            .ToList();

        return new GameStatisticsDto(
            request.GameId,
            gameName,
            totalPlays,
            wins,
            winRate,
            avgScore,
            highScore,
            avgDuration.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture),
            scoreHistory);
    }
}

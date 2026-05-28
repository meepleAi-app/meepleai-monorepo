using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;

/// <summary>
/// Handles retrieving cross-game statistics for a player.
/// Issue #3890: CQRS queries for play records - MVP statistics.
/// </summary>
internal class GetPlayerStatisticsQueryHandler : IQueryHandler<GetPlayerStatisticsQuery, PlayerStatisticsDto>
{
    private readonly MeepleAiDbContext _context;

    public GetPlayerStatisticsQueryHandler(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PlayerStatisticsDto> Handle(
        GetPlayerStatisticsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var recordsQuery = _context.PlayRecords
            .AsNoTracking()
            .Include(r => r.Players)
                .ThenInclude(p => p.Scores)
            .Where(r => r.CreatedByUserId == query.UserId
                        && r.Status == (int)PlayRecordStatus.Completed);

        // Apply date filters if provided
        if (query.StartDate.HasValue)
        {
            recordsQuery = recordsQuery.Where(r => r.SessionDate >= query.StartDate.Value);
        }

        if (query.EndDate.HasValue)
        {
            recordsQuery = recordsQuery.Where(r => r.SessionDate <= query.EndDate.Value);
        }

        var records = await recordsQuery.ToListAsync(cancellationToken).ConfigureAwait(false);

        // Calculate statistics
        var totalSessions = records.Count;

        // Total wins: reuse PlayRecordOutcomeCalculator.HasWinner (DRY — avoids duplicating "wins">0 logic)
        var totalWins = records.Count(r => PlayRecordOutcomeCalculator.HasWinner(r.Players));

        // Game play counts (legacy dict — backward compat)
        var gamePlayCounts = records
            .GroupBy(r => r.GameName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Count(),
                StringComparer.OrdinalIgnoreCase);

        // Average scores by game (for "points" dimension — legacy dict — backward compat)
        var averageScoresByGame = records
            .Where(r => r.Players.Any(p => p.Scores.Any(s =>
                s.Dimension.Equals("points", StringComparison.OrdinalIgnoreCase))))
            .GroupBy(r => r.GameName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(r => r.Players
                        .SelectMany(p => p.Scores
                            .Where(s => s.Dimension.Equals("points", StringComparison.OrdinalIgnoreCase))
                            .Select(s => (double)s.Value)))
                    .DefaultIfEmpty(0)
                    .Average(),
                StringComparer.OrdinalIgnoreCase);

        // Total duration: sum nullable durations; null contributes 0
        var totalDurationMinutes = (int)Math.Round(
            records.Sum(r => r.Duration?.TotalMinutes ?? 0));

        // Group by (GameId, GameName) for win-rate and play-count breakdowns.
        // Free-form records (GameId == null) group by (null, GameName) so two records
        // with the same free-form name aggregate together — per spec.
        var byGame = records
            .GroupBy(r => (r.GameId, r.GameName))
            .ToList();

        var winByGame = byGame
            .Select(g => new GameWinStats(
                GameId: g.Key.GameId,
                GameName: g.Key.GameName,
                Played: g.Count(),
                Won: g.Count(r => PlayRecordOutcomeCalculator.HasWinner(r.Players))))
            .OrderByDescending(x => x.Played)
            .ThenByDescending(x => x.Won)
            .ToList()
            .AsReadOnly();

        var mostPlayedGames = byGame
            .Select(g => new GamePlayCount(
                GameId: g.Key.GameId,
                GameName: g.Key.GameName,
                Plays: g.Count()))
            .OrderByDescending(x => x.Plays)
            .ToList()
            .AsReadOnly();

        return new PlayerStatisticsDto(
            totalSessions,
            totalWins,
            gamePlayCounts,
            averageScoresByGame,
            totalDurationMinutes,
            winByGame,
            mostPlayedGames
        );
    }
}

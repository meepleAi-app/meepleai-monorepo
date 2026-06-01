using System.Globalization;
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
/// Issue #1540 / #1541 / #1550: extends DTO with leaderboardRank, favoriteAgentName,
/// winRateTrend for /players/[id] v2 surface.
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

        // #1540: LeaderboardRank — rank of current user among ALL users by completed
        // wins (desc). Tie-breaker not specified; users with identical win counts
        // share the same rank position. NULL when the user has 0 completed sessions
        // (no participation = unranked).
        //
        // Implementation: count users with strictly MORE wins than the caller, add 1.
        // Wins predicate mirrors `PlayRecordOutcomeCalculator.HasWinner`
        // (any player has a "wins" score > 0) but expressed as an EF-translatable
        // lambda so the aggregation runs in SQL, not in memory.
        //
        // Scale note (MVP): per-user grouping over completed records is O(n) in record
        // count. Acceptable for community scale < 100k users; revisit with a materialized
        // leaderboard view when scale demands.
        int? leaderboardRank = null;
        if (totalSessions > 0)
        {
            var usersWithMoreWins = await _context.PlayRecords
                .AsNoTracking()
                .Where(r => r.Status == (int)PlayRecordStatus.Completed)
                .Where(r => r.Players.Any(p => p.Scores.Any(s =>
                    s.Dimension == "wins" && s.Value > 0)))
                .GroupBy(r => r.CreatedByUserId)
                .Select(g => new { UserId = g.Key, Wins = g.Count() })
                .Where(x => x.UserId != query.UserId && x.Wins > totalWins)
                .CountAsync(cancellationToken)
                .ConfigureAwait(false);

            leaderboardRank = usersWithMoreWins + 1;
        }

        // #1541: FavoriteAgentName — most used agent by chat-thread count for this
        // user. Cross-context lookup via the monolithic MeepleAiDbContext (logical
        // cross-context coupling, physical single DbContext is acceptable). Returns
        // NULL when the user has 0 chat threads with an associated AgentId.
        string? favoriteAgentName = null;
        var favoriteAgentIdInfo = await _context.ChatThreads
            .AsNoTracking()
            .Where(t => t.UserId == query.UserId && t.AgentId != null)
            .GroupBy(t => t.AgentId)
            .Select(g => new { AgentId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.AgentId) // deterministic tie-breaker
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (favoriteAgentIdInfo?.AgentId != null)
        {
            favoriteAgentName = await _context.AgentDefinitions
                .AsNoTracking()
                .Where(a => a.Id == favoriteAgentIdInfo.AgentId)
                .Select(a => a.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        // #1550: WinRateTrend — last 6 ISO months (YYYY-MM) ending with the current
        // month. Months with zero completed sessions are EXCLUDED (per AC: "new users
        // with < 6 months → return what's available"). Win rate per month = wins/plays
        // in [0, 1]; months with plays but 0 wins return 0.
        //
        // Aggregation done in-memory over the already-loaded `records` collection
        // (no extra DB round-trip). Filters records to the 6-month window first to
        // avoid surfacing very old data.
        var monthCutoff = new DateTime(
            DateTime.UtcNow.Year,
            DateTime.UtcNow.Month,
            1,
            0, 0, 0,
            DateTimeKind.Utc).AddMonths(-5); // 6-month inclusive sliding window

        // Group key uses StringComparer.Ordinal — the month key is a synthetic ISO
        // bucket (YYYY-MM) that must compare byte-for-byte, not culture-sensitively.
        var winRateTrend = records
            .Where(r => r.SessionDate >= monthCutoff)
            .GroupBy(r => $"{r.SessionDate.Year:D4}-{r.SessionDate.Month:D2}", StringComparer.Ordinal)
            .Select(g =>
            {
                // A GroupBy result group always contains ≥1 element by construction,
                // so g.Count() > 0 is always true — guard removed per analyzer.
                var played = g.Count();
                var won = g.Count(r => PlayRecordOutcomeCalculator.HasWinner(r.Players));
                return new MonthlyWinRate(g.Key, won / (double)played);
            })
            .OrderBy(x => x.Month, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();

        return new PlayerStatisticsDto(
            totalSessions,
            totalWins,
            gamePlayCounts,
            averageScoresByGame,
            totalDurationMinutes,
            winByGame,
            mostPlayedGames,
            leaderboardRank,
            favoriteAgentName,
            winRateTrend
        );
    }
}

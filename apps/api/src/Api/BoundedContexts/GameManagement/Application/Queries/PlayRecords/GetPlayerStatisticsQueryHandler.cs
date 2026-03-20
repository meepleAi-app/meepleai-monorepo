using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
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

        // Total wins (count records where user has win score dimension)
        var totalWins = records.Count(r =>
            r.Players.Any(p => p.Scores.Any(s =>
                s.Dimension.Equals("wins", StringComparison.OrdinalIgnoreCase) && s.Value > 0)));

        // Game play counts
        var gamePlayCounts = records
            .GroupBy(r => r.GameName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Count(),
                StringComparer.OrdinalIgnoreCase);

        // Average scores by game (for "points" dimension)
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

        return new PlayerStatisticsDto(
            totalSessions,
            totalWins,
            gamePlayCounts,
            averageScoresByGame
        );
    }
}

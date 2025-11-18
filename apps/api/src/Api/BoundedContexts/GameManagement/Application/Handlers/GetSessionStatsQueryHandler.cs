using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles query to get aggregated session statistics.
/// </summary>
public class GetSessionStatsQueryHandler : IQueryHandler<GetSessionStatsQuery, SessionStatsDto>
{
    private readonly IGameSessionRepository _sessionRepository;

    public GetSessionStatsQueryHandler(IGameSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    public async Task<SessionStatsDto> Handle(GetSessionStatsQuery query, CancellationToken cancellationToken)
    {
        // Retrieve all historical sessions matching filters (no pagination for stats)
        var sessions = await _sessionRepository.FindHistoryAsync(
            gameId: query.GameId,
            startDate: query.StartDate,
            endDate: query.EndDate,
            limit: null,
            offset: null,
            cancellationToken: cancellationToken
        );

        // Calculate basic statistics
        var totalSessions = sessions.Count;
        var completedSessions = sessions.Count(s => s.Status == SessionStatus.Completed);
        var abandonedSessions = sessions.Count(s => s.Status == SessionStatus.Abandoned);

        var averageDuration = sessions.Any()
            ? (int)Math.Round(sessions.Average(s => s.DurationMinutes))
            : 0;

        // Calculate win statistics
        var winCounts = new Dictionary<string, int>();

        foreach (var session in sessions)
        {
            if (session.Status == SessionStatus.Completed && !string.IsNullOrWhiteSpace(session.WinnerName))
            {
                var winnerName = session.WinnerName.Trim();
                if (!winCounts.ContainsKey(winnerName))
                    winCounts[winnerName] = 0;
                winCounts[winnerName]++;
            }
        }

        // Build top players list
        var topPlayers = winCounts
            .OrderByDescending(kvp => kvp.Value)
            .Take(query.TopPlayersLimit)
            .Select(kvp => new PlayerWinStatsDto(
                PlayerName: kvp.Key,
                WinCount: kvp.Value,
                WinRate: completedSessions > 0
                    ? Math.Round((decimal)kvp.Value / completedSessions * 100, 2)
                    : 0m
            ))
            .ToList();

        return new SessionStatsDto(
            TotalSessions: totalSessions,
            CompletedSessions: completedSessions,
            AbandonedSessions: abandonedSessions,
            AverageDurationMinutes: averageDuration,
            TopPlayers: topPlayers
        );
    }
}

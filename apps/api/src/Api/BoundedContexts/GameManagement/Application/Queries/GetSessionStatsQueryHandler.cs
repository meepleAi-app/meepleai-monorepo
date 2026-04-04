using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Handles query to get aggregated session statistics.
/// </summary>
internal class GetSessionStatsQueryHandler : IQueryHandler<GetSessionStatsQuery, SessionStatsDto>
{
    private readonly IGameSessionRepository _sessionRepository;

    public GetSessionStatsQueryHandler(IGameSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<SessionStatsDto> Handle(GetSessionStatsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // Retrieve all historical sessions matching filters (no pagination for stats)
        var sessions = await _sessionRepository.FindHistoryAsync(
            gameId: query.GameId,
            startDate: query.StartDate,
            endDate: query.EndDate,
            limit: null,
            offset: null,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        // Calculate basic statistics
        var totalSessions = sessions.Count;
        var completedSessions = sessions.Count(s => s.Status == SessionStatus.Completed);
        var abandonedSessions = sessions.Count(s => s.Status == SessionStatus.Abandoned);

        var averageDuration = sessions.Count > 0
            ? (int)Math.Round(sessions.Average(s => s.Duration.TotalMinutes))
            : 0;

        // Calculate win statistics
        // Use case-insensitive dictionary to properly aggregate wins for same player regardless of casing
        var winCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var originalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var session in sessions)
        {
            if (session.Status == SessionStatus.Completed && !string.IsNullOrWhiteSpace(session.WinnerName))
            {
                var winnerName = session.WinnerName.Trim();

                // Store original casing from first occurrence for display
                originalNames.TryAdd(winnerName, winnerName);

                if (!winCounts.TryGetValue(winnerName, out var count))
                    winCounts[winnerName] = 1;
                else
                    winCounts[winnerName] = count + 1;
            }
        }

        // Build top players list using original name casing for display
        var topPlayers = winCounts
            .OrderByDescending(kvp => kvp.Value)
            .Take(query.TopPlayersLimit)
            .Select(kvp => new PlayerWinStatsDto(
                PlayerName: originalNames[kvp.Key],
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

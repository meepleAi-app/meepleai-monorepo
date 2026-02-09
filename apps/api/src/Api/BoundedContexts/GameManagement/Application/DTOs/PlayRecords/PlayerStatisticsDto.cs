namespace Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;

/// <summary>
/// DTO for cross-game player statistics.
/// Issue #3890: CQRS queries for play records.
/// </summary>
public record PlayerStatisticsDto(
    int TotalSessions,
    int TotalWins,
    Dictionary<string, int> GamePlayCounts,
    Dictionary<string, double> AverageScoresByGame
);

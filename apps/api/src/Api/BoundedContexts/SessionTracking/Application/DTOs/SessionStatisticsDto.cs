namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

public record SessionStatisticsDto(
    int TotalSessions,
    int TotalGamesPlayed,
    string AverageSessionDuration,
    List<GamePlayFrequencyDto> MostPlayedGames,
    List<ScoreTrendDto> RecentScoreTrends,
    List<MonthlyPlayCountDto> MonthlyActivity);

public record GamePlayFrequencyDto(Guid GameId, string GameName, int PlayCount);

public record ScoreTrendDto(DateTime Date, string GameName, decimal FinalScore);

public record MonthlyPlayCountDto(string Month, int SessionCount);

public record GameStatisticsDto(
    Guid GameId,
    string GameName,
    int TotalPlays,
    int Wins,
    double WinRate,
    double AverageScore,
    decimal HighScore,
    string AverageSessionDuration,
    List<ScoreTrendDto> ScoreHistory);

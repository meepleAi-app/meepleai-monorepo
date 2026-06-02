namespace Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;

/// <summary>
/// DTO for cross-game player statistics.
/// Issue #3890: CQRS queries for play records.
/// Issue #1663: Phase 2 – statistics dashboard fields.
/// Issue #1540 / #1541 / #1550: leaderboardRank, favoriteAgentName, winRateTrend.
/// </summary>
public record PlayerStatisticsDto(
    int TotalSessions,
    int TotalWins,
    Dictionary<string, int> GamePlayCounts,
    Dictionary<string, double> AverageScoresByGame,
    int TotalDurationMinutes,
    IReadOnlyList<GameWinStats> WinByGame,
    IReadOnlyList<GamePlayCount> MostPlayedGames,
    int? LeaderboardRank,
    string? FavoriteAgentName,
    IReadOnlyList<MonthlyWinRate> WinRateTrend
);

/// <summary>
/// Per-game win/play breakdown, used in <see cref="PlayerStatisticsDto.WinByGame"/>.
/// Issue #1663: Phase 2 – statistics dashboard fields.
/// </summary>
public record GameWinStats(Guid? GameId, string GameName, int Played, int Won);

/// <summary>
/// Per-game play count, used in <see cref="PlayerStatisticsDto.MostPlayedGames"/>.
/// Issue #1663: Phase 2 – statistics dashboard fields.
/// </summary>
public record GamePlayCount(Guid? GameId, string GameName, int Plays);

/// <summary>
/// Win rate for a single ISO month bucket (YYYY-MM), used in
/// <see cref="PlayerStatisticsDto.WinRateTrend"/>. Win rate is in [0, 1].
/// Issue #1550: 6-month sliding window for PlayerTrendCard.
/// </summary>
public record MonthlyWinRate(string Month, double WinRate);

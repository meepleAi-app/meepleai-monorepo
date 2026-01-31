using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.UserLibrary.Domain.ValueObjects;

/// <summary>
/// Value object representing gameplay statistics for a game in user's library.
/// Tracks play count, last played date, win rate, and average duration.
/// </summary>
internal sealed class GameStats : ValueObject
{
    /// <summary>
    /// Total number of times the game has been played.
    /// </summary>
    public int TimesPlayed { get; }

    /// <summary>
    /// Date and time when the game was last played (null if never played).
    /// </summary>
    public DateTime? LastPlayed { get; }

    /// <summary>
    /// Win rate as a percentage (0-100). Null if no competitive sessions recorded.
    /// </summary>
    public decimal? WinRate { get; }

    /// <summary>
    /// Average duration of a game session in minutes. Null if no sessions recorded.
    /// </summary>
    public int? AvgDuration { get; }

    /// <summary>
    /// Total number of competitive sessions played (where didWin was not null).
    /// Used internally for accurate win rate calculation.
    /// </summary>
    private int CompetitiveSessions { get; }

    /// <summary>
    /// Private constructor for creating GameStats instances.
    /// </summary>
    /// <param name="timesPlayed">Total play count</param>
    /// <param name="lastPlayed">Last played timestamp</param>
    /// <param name="winRate">Win rate percentage (0-100)</param>
    /// <param name="avgDuration">Average duration in minutes</param>
    /// <param name="competitiveSessions">Number of competitive sessions</param>
    /// <exception cref="ArgumentException">Thrown when invalid values are provided</exception>
    private GameStats(int timesPlayed, DateTime? lastPlayed, decimal? winRate, int? avgDuration, int competitiveSessions = 0)
    {
        if (timesPlayed < 0)
            throw new ArgumentException("TimesPlayed cannot be negative", nameof(timesPlayed));

        if (winRate.HasValue && (winRate < 0 || winRate > 100))
            throw new ArgumentException("WinRate must be between 0 and 100", nameof(winRate));

        if (avgDuration.HasValue && avgDuration < 0)
            throw new ArgumentException("AvgDuration cannot be negative", nameof(avgDuration));

        // Validate logical consistency: if never played, no stats should exist
        if (timesPlayed == 0)
        {
            if (lastPlayed.HasValue)
                throw new ArgumentException("LastPlayed must be null if TimesPlayed is 0", nameof(lastPlayed));
            if (winRate.HasValue)
                throw new ArgumentException("WinRate must be null if TimesPlayed is 0", nameof(winRate));
            if (avgDuration.HasValue)
                throw new ArgumentException("AvgDuration must be null if TimesPlayed is 0", nameof(avgDuration));
        }

        TimesPlayed = timesPlayed;
        LastPlayed = lastPlayed;
        WinRate = winRate;
        AvgDuration = avgDuration;
        CompetitiveSessions = competitiveSessions;
    }

    /// <summary>
    /// Creates initial stats for a game that has never been played.
    /// </summary>
    public static GameStats Empty()
        => new(timesPlayed: 0, lastPlayed: null, winRate: null, avgDuration: null);

    /// <summary>
    /// Creates stats with specified values (for reconstruction from database).
    /// </summary>
    public static GameStats Create(
        int timesPlayed,
        DateTime? lastPlayed,
        decimal? winRate,
        int? avgDuration,
        int competitiveSessions = 0)
        => new(timesPlayed, lastPlayed, winRate, avgDuration, competitiveSessions);

    /// <summary>
    /// Records a new game session and updates stats.
    /// </summary>
    /// <param name="durationMinutes">Duration of the session in minutes</param>
    /// <param name="didWin">Whether the user won this session (null for non-competitive games)</param>
    /// <param name="playedAt">When the session was played</param>
    /// <returns>New GameStats instance with updated values</returns>
    public GameStats RecordSession(int durationMinutes, bool? didWin, DateTime playedAt)
    {
        if (durationMinutes < 0)
            throw new ArgumentException("Duration cannot be negative", nameof(durationMinutes));

        var newTimesPlayed = TimesPlayed + 1;
        var newLastPlayed = playedAt;

        // Calculate new average duration
        int? newAvgDuration;
        if (AvgDuration.HasValue)
        {
            // Weighted average: (old_avg * old_count + new_duration) / new_count
            newAvgDuration = (AvgDuration.Value * TimesPlayed + durationMinutes) / newTimesPlayed;
        }
        else
        {
            newAvgDuration = durationMinutes;
        }

        // Calculate new win rate (only if competitive session)
        decimal? newWinRate = WinRate;
        var newCompetitiveSessions = CompetitiveSessions;

        if (didWin.HasValue)
        {
            newCompetitiveSessions = CompetitiveSessions + 1;

            if (WinRate.HasValue)
            {
                // Recalculate based on competitive sessions only
                var previousWins = (WinRate.Value / 100m) * CompetitiveSessions;
                var newWins = previousWins + (didWin.Value ? 1 : 0);
                newWinRate = (newWins / newCompetitiveSessions) * 100m;
            }
            else
            {
                // First competitive session
                newWinRate = didWin.Value ? 100m : 0m;
            }
        }

        return new GameStats(newTimesPlayed, newLastPlayed, newWinRate, newAvgDuration, newCompetitiveSessions);
    }

    /// <summary>
    /// Returns whether any sessions have been recorded.
    /// </summary>
    public bool HasPlayHistory() => TimesPlayed > 0;

    /// <summary>
    /// Returns whether competitive stats (win rate) are available.
    /// </summary>
    public bool HasCompetitiveStats() => WinRate.HasValue;

    /// <summary>
    /// Gets a formatted string representation of the win rate.
    /// </summary>
    public string GetWinRateFormatted() => WinRate.HasValue ? $"{WinRate:0}%" : "N/A";

    /// <summary>
    /// Gets a human-readable duration string (e.g., "2h 30m").
    /// </summary>
    public string GetAvgDurationFormatted()
    {
        if (!AvgDuration.HasValue)
            return "N/A";

        var hours = AvgDuration.Value / 60;
        var minutes = AvgDuration.Value % 60;

        if (hours > 0)
            return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";

        return $"{minutes}m";
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TimesPlayed;
        yield return LastPlayed;
        yield return WinRate;
        yield return AvgDuration;
        yield return CompetitiveSessions;
    }

    public override string ToString()
        => $"Played: {TimesPlayed}, Win Rate: {GetWinRateFormatted()}, Avg: {GetAvgDurationFormatted()}";
}

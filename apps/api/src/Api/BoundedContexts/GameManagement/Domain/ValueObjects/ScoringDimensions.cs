namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Canonical scoring dimension names used by play-record aggregations (statistics, leaderboard).
/// Matches the dimensions produced by <see cref="RecordScore.Wins"/> and <see cref="RecordScore.Points"/>.
/// </summary>
internal static class ScoringDimensions
{
    /// <summary>
    /// Win count for a record (typically 0 or 1, but the model permits higher values, e.g. a
    /// best-of series). The leaderboard SUMS this dimension across a player's records to get
    /// total wins — intentional, to mirror the "total wins" figure in the game-detail mockup.
    /// </summary>
    public const string Wins = "wins";

    /// <summary>Numeric score; the leaderboard averages this dimension across a player's records.</summary>
    public const string Points = "points";
}

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Canonical scoring dimension names used by play-record aggregations (statistics, leaderboard).
/// Matches the dimensions produced by <see cref="RecordScore.Wins"/> and <see cref="RecordScore.Points"/>.
/// </summary>
internal static class ScoringDimensions
{
    public const string Wins = "wins";
    public const string Points = "points";
}

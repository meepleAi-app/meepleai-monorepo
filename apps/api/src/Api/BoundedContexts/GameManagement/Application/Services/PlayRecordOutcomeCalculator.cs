using Api.Infrastructure.Entities.GameManagement;

namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Pure static helper that computes outcome fields from EF infrastructure entities.
/// Operates on <see cref="RecordPlayerEntity"/> and <see cref="RecordScoreEntity"/> —
/// no EF queries, no async — safe to call inside any handler mapping.
///
/// Dimension convention (documented in GetPlayerStatisticsQueryHandler):
///   "wins"   – victory flag: Value &gt; 0 means this player won the competitive game.
///   "points" – primary numeric score: the player's raw point total.
///
/// Issue #1663: Phase 1 – reskin-required fields computed on read.
/// </summary>
internal static class PlayRecordOutcomeCalculator
{
    private const string WinsDimension = "wins";
    private const string PointsDimension = "points";

    /// <summary>
    /// Returns the IDs of all players who have a "wins" score with Value &gt; 0.
    /// Returns an empty list when no player has the "wins" dimension (non-competitive / in-progress).
    /// </summary>
    public static IReadOnlyList<Guid> WinnerPlayerIds(IEnumerable<RecordPlayerEntity> players)
    {
        ArgumentNullException.ThrowIfNull(players);

        return players
            .Where(p => p.Scores.Any(s =>
                string.Equals(s.Dimension, WinsDimension, StringComparison.OrdinalIgnoreCase)
                && s.Value > 0))
            .Select(p => p.Id)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Returns <c>"competitive"</c> if ANY player has a score with Dimension "wins"
    /// (regardless of value — mere presence signals a competitive game).
    /// Returns <c>"none"</c> when no player has the "wins" dimension (cooperative / narrative / unscored).
    /// </summary>
    public static string OutcomeType(IEnumerable<RecordPlayerEntity> players)
    {
        ArgumentNullException.ThrowIfNull(players);

        var hasWinsDimension = players.Any(p =>
            p.Scores.Any(s =>
                string.Equals(s.Dimension, WinsDimension, StringComparison.OrdinalIgnoreCase)));

        return hasWinsDimension ? "competitive" : "none";
    }

    /// <summary>
    /// Returns <c>true</c> when at least one player in the collection has a "wins" score
    /// with <see cref="RecordScoreEntity.Value"/> &gt; 0.
    /// Avoids allocating a list; use this instead of <see cref="WinnerPlayerIds"/> when
    /// only the boolean result is needed (e.g. counting wins across many records).
    /// Issue #1663: Phase 2 – statistics dashboard fields.
    /// </summary>
    public static bool HasWinner(IEnumerable<RecordPlayerEntity> players)
    {
        ArgumentNullException.ThrowIfNull(players);

        return players.Any(p => p.Scores.Any(s =>
            string.Equals(s.Dimension, WinsDimension, StringComparison.OrdinalIgnoreCase)
            && s.Value > 0));
    }

    /// <summary>
    /// Returns the "points" dimension <see cref="RecordScoreEntity.Value"/> for the given player,
    /// or <c>null</c> if the player has no "points" score recorded.
    /// </summary>
    public static int? TotalScore(RecordPlayerEntity player)
    {
        ArgumentNullException.ThrowIfNull(player);

        var pointsScore = player.Scores.FirstOrDefault(s =>
            string.Equals(s.Dimension, PointsDimension, StringComparison.OrdinalIgnoreCase));

        return pointsScore?.Value;
    }
}

namespace Api.BoundedContexts.AgentMemory.Domain.Models;

/// <summary>
/// Tracks a player's win/loss/score statistics for a specific game.
/// </summary>
internal sealed class PlayerGameStats
{
    private PlayerGameStats() { } // Required for JSON deserialization and EF

    public Guid GameId { get; private set; }
    public int Wins { get; private set; }
    public int Losses { get; private set; }
    public int TotalPlayed { get; private set; }
    public int? BestScore { get; private set; }

    public static PlayerGameStats Create(Guid gameId, bool won, int? score = null)
    {
        if (gameId == Guid.Empty) throw new ArgumentException("GameId cannot be empty.", nameof(gameId));

        return new PlayerGameStats
        {
            GameId = gameId,
            TotalPlayed = 1,
            Wins = won ? 1 : 0,
            Losses = won ? 0 : 1,
            BestScore = score
        };
    }

    /// <summary>
    /// Records an additional play, updating win/loss counts and best score.
    /// </summary>
    public void RecordPlay(bool won, int? score)
    {
        TotalPlayed++;
        if (won) Wins++;
        else Losses++;

        if (score.HasValue && (BestScore == null || score.Value > BestScore.Value))
            BestScore = score.Value;
    }

    /// <summary>
    /// Restores all fields from persistence (used by repository when reconstructing from JSONB).
    /// </summary>
    internal static PlayerGameStats Restore(Guid gameId, int wins, int losses, int totalPlayed, int? bestScore)
    {
        return new PlayerGameStats
        {
            GameId = gameId,
            Wins = wins,
            Losses = losses,
            TotalPlayed = totalPlayed,
            BestScore = bestScore
        };
    }
}

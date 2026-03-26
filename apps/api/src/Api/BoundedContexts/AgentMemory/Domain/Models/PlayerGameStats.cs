namespace Api.BoundedContexts.AgentMemory.Domain.Models;

/// <summary>
/// Tracks a player's win/loss/score statistics for a specific game.
/// </summary>
internal sealed class PlayerGameStats
{
    public Guid GameId { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int TotalPlayed { get; set; }
    public int? BestScore { get; set; }
}

namespace Api.Infrastructure.Entities;

/// <summary>
/// EF Core entity for game session persistence.
/// Represents a play session of a board game.
/// </summary>
internal class GameSessionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public string Status { get; set; } = "Setup"; // Setup, InProgress, Completed, Abandoned
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? WinnerName { get; set; }
    public string? Notes { get; set; }

    // Players stored as JSON array (e.g., [{"PlayerName":"Alice","PlayerOrder":1,"Color":"Red"}])
    public string PlayersJson { get; set; } = "[]";

    // Navigation properties
    public GameEntity Game { get; set; } = null!;
}

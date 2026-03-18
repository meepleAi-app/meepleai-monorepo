namespace Api.BoundedContexts.AgentMemory.Infrastructure.Entities;

/// <summary>
/// Infrastructure entity for GameMemory aggregate.
/// Maps domain GameMemory to database table for per-game, per-owner memory persistence.
/// </summary>
public class GameMemoryEntity
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Guid OwnerId { get; set; }
    public string? HouseRulesJson { get; set; }  // JSONB
    public string? CustomSetupJson { get; set; }  // JSONB
    public string? NotesJson { get; set; }  // JSONB
    public DateTime CreatedAt { get; set; }
}

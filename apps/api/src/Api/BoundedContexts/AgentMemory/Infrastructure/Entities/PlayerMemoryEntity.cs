namespace Api.BoundedContexts.AgentMemory.Infrastructure.Entities;

/// <summary>
/// Infrastructure entity for PlayerMemory aggregate.
/// Maps domain PlayerMemory to database table for player game statistics persistence.
/// </summary>
public class PlayerMemoryEntity
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? GuestName { get; set; }
    public Guid? GroupId { get; set; }
    public string? GameStatsJson { get; set; }  // JSONB
    public DateTime? ClaimedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

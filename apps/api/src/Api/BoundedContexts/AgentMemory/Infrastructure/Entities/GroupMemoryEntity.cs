namespace Api.BoundedContexts.AgentMemory.Infrastructure.Entities;

/// <summary>
/// Infrastructure entity for GroupMemory aggregate.
/// Maps domain GroupMemory to database table for play group memory persistence.
/// </summary>
public class GroupMemoryEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CreatorId { get; set; }
    public string? MembersJson { get; set; }  // JSONB
    public string? PreferencesJson { get; set; }  // JSONB
    public string? StatsJson { get; set; }  // JSONB
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

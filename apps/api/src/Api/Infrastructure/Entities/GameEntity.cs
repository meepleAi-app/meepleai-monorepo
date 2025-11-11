namespace Api.Infrastructure.Entities;

public class GameEntity
{
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // BoardGameGeek integration (AI-13)
    public int? BggId { get; set; }
    public string? BggMetadata { get; set; } // JSONB column storing raw BGG API response

    public ICollection<RuleSpecEntity> RuleSpecs { get; set; } = new List<RuleSpecEntity>();
    public ICollection<AgentEntity> Agents { get; set; } = new List<AgentEntity>();
    public ICollection<ChatEntity> Chats { get; set; } = new List<ChatEntity>();
}

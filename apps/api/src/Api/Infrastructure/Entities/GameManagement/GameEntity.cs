namespace Api.Infrastructure.Entities;

public class GameEntity
{
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Game Details (DDD-PHASE2 GameManagement bounded context)
    public string? Publisher { get; set; }
    public int? YearPublished { get; set; }
    public int? MinPlayers { get; set; }
    public int? MaxPlayers { get; set; }
    public int? MinPlayTimeMinutes { get; set; }
    public int? MaxPlayTimeMinutes { get; set; }

    // BoardGameGeek integration (AI-13)
    public int? BggId { get; set; }
    public string? BggMetadata { get; set; } // JSONB column storing raw BGG API response

    // Admin Wizard: Game images
    public string? IconUrl { get; set; }
    public string? ImageUrl { get; set; }

    public ICollection<RuleSpecEntity> RuleSpecs { get; set; } = new List<RuleSpecEntity>();
    public ICollection<AgentEntity> Agents { get; set; } = new List<AgentEntity>();
    public ICollection<ChatEntity> Chats { get; set; } = new List<ChatEntity>();
}

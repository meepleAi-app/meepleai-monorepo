namespace Api.Infrastructure.Entities;

public class AgentEntity
{
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid Id { get; set; } = Guid.NewGuid();
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid GameId { get; set; }
    public string Name { get; set; } = default!;
    public string Kind { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public GameEntity Game { get; set; } = default!;
    public ICollection<ChatEntity> Chats { get; set; } = new List<ChatEntity>();
}

namespace Api.Infrastructure.Entities;

public class ChatEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid UserId { get; set; }
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid GameId { get; set; }
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid AgentId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastMessageAt { get; set; }

    public UserEntity User { get; set; } = default!;
    public GameEntity Game { get; set; } = default!;
    public AgentEntity Agent { get; set; } = default!;
    public ICollection<ChatLogEntity> Logs { get; set; } = new List<ChatLogEntity>();
}

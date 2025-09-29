namespace Api.Infrastructure.Entities;

public class TenantEntity
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
        = DateTime.UtcNow;

    public ICollection<UserEntity> Users { get; set; } = new List<UserEntity>();
    public ICollection<GameEntity> Games { get; set; } = new List<GameEntity>();
    public ICollection<RuleSpecEntity> RuleSpecs { get; set; } = new List<RuleSpecEntity>();
    public ICollection<AgentEntity> Agents { get; set; } = new List<AgentEntity>();
    public ICollection<ChatEntity> Chats { get; set; } = new List<ChatEntity>();
    public ICollection<ChatLogEntity> ChatLogs { get; set; } = new List<ChatLogEntity>();
}

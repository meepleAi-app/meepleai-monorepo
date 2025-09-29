namespace Api.Infrastructure.Entities;

public class ChatEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = default!;
    public string GameId { get; set; } = default!;
    public string AgentId { get; set; } = default!;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public TenantEntity Tenant { get; set; } = default!;
    public GameEntity Game { get; set; } = default!;
    public AgentEntity Agent { get; set; } = default!;
    public ICollection<ChatLogEntity> Logs { get; set; } = new List<ChatLogEntity>();
}

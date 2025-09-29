namespace Api.Infrastructure.Entities;

public class RuleSpecEntity
{
    public Guid Id { get; set; }
        = Guid.NewGuid();
    public string TenantId { get; set; } = default!;
    public string GameId { get; set; } = default!;
    public string Version { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TenantEntity Tenant { get; set; } = default!;
    public GameEntity Game { get; set; } = default!;
    public ICollection<RuleAtomEntity> Atoms { get; set; } = new List<RuleAtomEntity>();
}

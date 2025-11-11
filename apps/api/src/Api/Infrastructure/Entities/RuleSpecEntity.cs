namespace Api.Infrastructure.Entities;

public class RuleSpecEntity
{
    public Guid Id { get; set; }
        = Guid.NewGuid();
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid GameId { get; set; }
    public string Version { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid? CreatedByUserId { get; set; }

    // EDIT-06: Version timeline and branching support
    public Guid? ParentVersionId { get; set; }
    public string? MergedFromVersionIds { get; set; } // Comma-separated GUIDs

    public GameEntity Game { get; set; } = default!;
    public UserEntity? CreatedBy { get; set; }
    public RuleSpecEntity? ParentVersion { get; set; }
    public ICollection<RuleAtomEntity> Atoms { get; set; } = new List<RuleAtomEntity>();
}

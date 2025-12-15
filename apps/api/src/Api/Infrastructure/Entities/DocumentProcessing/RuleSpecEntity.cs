using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities;

/// <summary>
/// Persistence entity for RuleSpec.
/// Issue #2055: Collaborative editing with optimistic concurrency control.
/// Uses RowVersion (ETag) to prevent lost updates when concurrent modifications occur.
/// </summary>
internal class RuleSpecEntity
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

    // Issue #2055: Optimistic concurrency control for collaborative editing
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public GameEntity Game { get; set; } = default!;
    public UserEntity? CreatedBy { get; set; }
    public RuleSpecEntity? ParentVersion { get; set; }
    public ICollection<RuleAtomEntity> Atoms { get; set; } = new List<RuleAtomEntity>();
}

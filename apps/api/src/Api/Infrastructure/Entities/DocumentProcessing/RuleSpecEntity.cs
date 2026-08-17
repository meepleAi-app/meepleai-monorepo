using Api.Infrastructure.Entities.SharedGameCatalog;
using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities;

/// <summary>
/// Persistence entity for RuleSpec.
/// Issue #2055: Collaborative editing with optimistic concurrency control.
/// Uses RowVersion (ETag) to prevent lost updates when concurrent modifications occur.
/// </summary>
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

    /// <summary>
    /// Concurrency token (#2055 per l'editing collaborativo, convertito a <c>xmin</c> da #3651).
    ///
    /// <para>
    /// Esce dal boundary HTTP come <c>ETag</c> in <c>RuleSpecDto</c>/<c>GameDto</c>, e i client lo
    /// rimandano in <c>UpdateRuleSpecCommand.ExpectedETag</c>. Prima era <c>[Timestamp] byte[]?</c>
    /// su una colonna <c>bytea</c> che Postgres non popola: l'ETag esposto era <b>sempre null</b> e
    /// il confronto lato server era racchiuso in una guardia <c>RowVersion != null</c> mai vera —
    /// l'editing collaborativo non ha quindi mai rilevato un conflitto.
    /// </para>
    /// </summary>
    public uint Xmin { get; set; }

    public SharedGameEntity Game { get; set; } = default!;
    public UserEntity? CreatedBy { get; set; }
    public RuleSpecEntity? ParentVersion { get; set; }
    public ICollection<RuleAtomEntity> Atoms { get; set; } = new List<RuleAtomEntity>();
}

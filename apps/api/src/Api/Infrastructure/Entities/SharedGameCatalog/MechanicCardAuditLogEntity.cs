namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence entity for <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Entities.MechanicCardAuditLog"/>
/// (ADR-051 audit trail, #527). Append-only.
/// </summary>
public class MechanicCardAuditLogEntity
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }

    /// <summary>Lowercase string: 'published' | 'suppressed' | 'unsuppressed' | 'revised' (DB CHECK).</summary>
    public string Action { get; set; } = "published";

    public Guid ActorId { get; set; }
    public DateTime OccurredAt { get; set; }

    /// <summary>Optional JSONB metadata (e.g. reason, previous_card_id).</summary>
    public string? Metadata { get; set; }

    // === Navigation ===
    public MechanicCardEntity Card { get; set; } = default!;
}

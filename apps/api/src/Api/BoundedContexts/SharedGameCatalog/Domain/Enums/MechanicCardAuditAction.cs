namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// Audit action recorded in <c>mechanic_card_audit_log</c> for a <see cref="Aggregates.MechanicCard"/>
/// lifecycle event (ADR-051 audit trail). Persisted as a lowercase string (DB CHECK constraint).
/// </summary>
public enum MechanicCardAuditAction
{
    /// <summary>Card first published from an approved analysis (#527).</summary>
    Published = 0,

    /// <summary>Card taken down (is_suppressed=true) — #529.</summary>
    Suppressed = 1,

    /// <summary>Card takedown lifted (is_suppressed=false) — #529.</summary>
    Unsuppressed = 2,

    /// <summary>A superseding card (version+1) was published to replace a suppressed predecessor.</summary>
    Revised = 3
}

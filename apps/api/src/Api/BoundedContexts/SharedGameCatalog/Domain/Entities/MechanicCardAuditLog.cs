using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Immutable audit-log entry for a <see cref="Aggregates.MechanicCard"/> lifecycle action
/// (ADR-051 audit trail). Written by the command handler in the same transaction as the card
/// mutation so there are never orphaned rows.
/// </summary>
public sealed class MechanicCardAuditLog : Entity<Guid>
{
    public Guid CardId { get; private set; }
    public MechanicCardAuditAction Action { get; private set; }
    public Guid ActorId { get; private set; }
    public DateTime OccurredAt { get; private set; }

    /// <summary>Optional JSONB metadata, e.g. <c>{ "reason": "...", "previous_card_id": "..." }</c>.</summary>
    public string? Metadata { get; private set; }

    /// <summary>EF Core / reconstitution constructor.</summary>
    private MechanicCardAuditLog() : base()
    {
    }

    private MechanicCardAuditLog(
        Guid id,
        Guid cardId,
        MechanicCardAuditAction action,
        Guid actorId,
        DateTime occurredAt,
        string? metadata)
        : base(id)
    {
        CardId = cardId;
        Action = action;
        ActorId = actorId;
        OccurredAt = occurredAt;
        Metadata = metadata;
    }

    /// <summary>Creates a new audit-log entry.</summary>
    public static MechanicCardAuditLog Create(
        Guid cardId,
        MechanicCardAuditAction action,
        Guid actorId,
        DateTime occurredAt,
        string? metadata = null)
    {
        if (cardId == Guid.Empty)
        {
            throw new ArgumentException("CardId cannot be empty.", nameof(cardId));
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("ActorId cannot be empty.", nameof(actorId));
        }

        return new MechanicCardAuditLog(Guid.NewGuid(), cardId, action, actorId, occurredAt, metadata);
    }

    /// <summary>Rehydrates from persistence (no validation).</summary>
    public static MechanicCardAuditLog Reconstitute(
        Guid id,
        Guid cardId,
        MechanicCardAuditAction action,
        Guid actorId,
        DateTime occurredAt,
        string? metadata) =>
        new(id, cardId, action, actorId, occurredAt, metadata);
}

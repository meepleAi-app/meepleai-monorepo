using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Raised when an admin rejects a catalog seed draft. Audit-retained
/// (soft-delete on the entity) but never copied into the public catalog.
/// </summary>
internal sealed class CatalogSeedRejectedEvent : DomainEventBase
{
    public Guid DraftId { get; }
    public Guid RejectedByUserId { get; }
    public string Reason { get; }

    public CatalogSeedRejectedEvent(Guid draftId, Guid rejectedByUserId, string reason)
    {
        DraftId = draftId;
        RejectedByUserId = rejectedByUserId;
        Reason = reason ?? string.Empty;
    }
}

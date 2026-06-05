using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Raised when an admin approves a catalog seed draft and a new
/// SharedGameCatalogEntry is created/upserted as a result.
/// </summary>
internal sealed class CatalogSeedApprovedEvent : DomainEventBase
{
    public Guid DraftId { get; }
    public Guid ResultingSharedGameId { get; }
    public Guid ApprovedByUserId { get; }

    public CatalogSeedApprovedEvent(Guid draftId, Guid resultingSharedGameId, Guid approvedByUserId)
    {
        DraftId = draftId;
        ResultingSharedGameId = resultingSharedGameId;
        ApprovedByUserId = approvedByUserId;
    }
}

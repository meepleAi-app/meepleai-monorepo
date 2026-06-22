using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Raised when the CatalogSeedFetchJob successfully fetches provider data
/// for a draft entry (Wikidata primary + optional BGG fallback merged).
/// Consumers may use this for SSE notification, audit, or metrics.
/// </summary>
internal sealed class CatalogSeedFetchedEvent : DomainEventBase
{
    public Guid DraftId { get; }
    public string ProviderUsed { get; }   // "wikidata" | "bgg" | "wikidata+bgg"
    public int FetchedFields { get; }

    public CatalogSeedFetchedEvent(Guid draftId, string providerUsed, int fetchedFields)
    {
        DraftId = draftId;
        ProviderUsed = providerUsed ?? throw new ArgumentNullException(nameof(providerUsed));
        FetchedFields = fetchedFields;
    }
}

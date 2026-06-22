using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Issue #1903 M6.2 — proxy query for the Wikidata SPARQL search exposed to the
/// admin catalog-seed UI. Wraps <c>WikidataCatalogProvider.FetchAsync</c> behind
/// MediatR so the routing layer stays CQRS-compliant (endpoints use only
/// <c>IMediator.Send</c>; no direct service injection).
/// </summary>
internal sealed record WikidataSearchQuery(string SearchTerm) : IQuery<WikidataSearchResult>;

/// <summary>
/// Result of a Wikidata search proxy call. Mirrors the shape of the underlying
/// <c>CatalogProviderResult</c> so the admin UI can render the same per-field
/// provenance preview it would for any other provider.
/// </summary>
/// <param name="Fields">
/// Logical field map (e.g. "title", "yearPublished"). Empty when no match was
/// found or the provider returned an error.
/// </param>
/// <param name="ErrorMessage">Non-null when the provider call failed.</param>
internal sealed record WikidataSearchResult(
    IReadOnlyDictionary<string, FieldProvenance> Fields,
    string? ErrorMessage);

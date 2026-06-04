using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Aggregates results from multiple <see cref="ICatalogProvider"/> implementations
/// using Wikidata as primary source with BGG as fallback for missing fields.
/// Spec: 2026-06-04-admin-catalog-seed-design.md §3.2 / §7.
/// Issue #1903 (Task M4.1).
/// </summary>
internal interface ICatalogSeedAggregator
{
    Task<CatalogSeedAggregationResult> FetchAsync(CatalogProviderQuery query, CancellationToken ct);
}

/// <summary>
/// Result of a catalog seed aggregation. <see cref="Provenance"/> contains merged fields
/// (primary wins on conflict). <see cref="ProviderUsed"/> tracks which provider(s) contributed:
/// "wikidata", "bgg", "wikidata+bgg", or "none".
/// </summary>
internal readonly record struct CatalogSeedAggregationResult(
    CatalogSeedProvenance Provenance,
    string ProviderUsed,
    string CombinedRawPayload);

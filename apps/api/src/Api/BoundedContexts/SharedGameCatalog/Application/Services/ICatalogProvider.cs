using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Strategy contract for a single catalog data provider (Wikidata, BGG, etc.).
/// Spec: 2026-06-04-admin-catalog-seed-design.md §3.2 / §7.
/// Issue #1903.
/// </summary>
internal interface ICatalogProvider
{
    /// <summary>Provider identifier: "wikidata" | "bgg".</summary>
    string Name { get; }

    Task<CatalogProviderResult> FetchAsync(CatalogProviderQuery query, CancellationToken ct);
}

/// <summary>
/// Query input for a provider lookup. At least one of <see cref="BggId"/>,
/// <see cref="WikidataQid"/> or <see cref="SearchTerm"/> must be set.
/// </summary>
internal sealed record CatalogProviderQuery(int? BggId, string? WikidataQid, string? SearchTerm);

/// <summary>
/// Provider lookup result. <see cref="Fields"/> maps logical field name
/// (e.g. "title", "yearPublished", "mechanics") to its <see cref="FieldProvenance"/>.
/// On error, <see cref="ErrorMessage"/> is non-null and <see cref="Fields"/> is empty.
/// </summary>
internal sealed record CatalogProviderResult(
    IReadOnlyDictionary<string, FieldProvenance> Fields,
    string? RawPayloadJson,
    string? ErrorMessage)
{
    public bool Success => ErrorMessage is null && Fields.Count > 0;
    public static CatalogProviderResult Empty(string error) =>
        new(new Dictionary<string, FieldProvenance>(StringComparer.Ordinal), null, error);
}

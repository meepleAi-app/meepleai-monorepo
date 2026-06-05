using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Default <see cref="ICatalogSeedAggregator"/> implementation: queries both providers
/// in parallel, merges results with Wikidata as primary and BGG as fallback for missing
/// fields, and produces a combined raw JSON payload for audit/debugging.
/// Spec: 2026-06-04-admin-catalog-seed-design.md §3.2 / §7.
/// Issue #1903 (Task M4.1).
/// </summary>
internal sealed class CatalogSeedAggregator : ICatalogSeedAggregator
{
    private readonly ICatalogProvider _wikidata;
    private readonly ICatalogProvider _bgg;
    private readonly ILogger<CatalogSeedAggregator> _logger;

    public CatalogSeedAggregator(
        ICatalogProvider wikidata,
        ICatalogProvider bgg,
        ILogger<CatalogSeedAggregator> logger)
    {
        ArgumentNullException.ThrowIfNull(wikidata);
        ArgumentNullException.ThrowIfNull(bgg);
        if (!string.Equals(wikidata.Name, "wikidata", StringComparison.Ordinal))
        {
            throw new ArgumentException("Expected primary=wikidata", nameof(wikidata));
        }
        if (!string.Equals(bgg.Name, "bgg", StringComparison.Ordinal))
        {
            throw new ArgumentException("Expected fallback=bgg", nameof(bgg));
        }
        _wikidata = wikidata;
        _bgg = bgg;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CatalogSeedAggregationResult> FetchAsync(CatalogProviderQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var primary = await _wikidata.FetchAsync(query, ct).ConfigureAwait(false);
        var fallback = await _bgg.FetchAsync(query, ct).ConfigureAwait(false);

        var merged = CatalogSeedProvenance.Merge(primary.Fields, fallback.Fields);

        var providerUsed = (primary.Success, fallback.Success) switch
        {
            (true, true) => "wikidata+bgg",
            (true, false) => "wikidata",
            (false, true) => "bgg",
            _ => "none",
        };

        var rawCombined = $"{{\"wikidata\":{primary.RawPayloadJson ?? "null"},\"bgg\":{fallback.RawPayloadJson ?? "null"}}}";

        _logger.LogInformation(
            "CatalogSeedAggregator merged provider={Provider} fieldCount={Count}",
            providerUsed,
            merged.Fields.Count);

        return new CatalogSeedAggregationResult(merged, providerUsed, rawCombined);
    }
}

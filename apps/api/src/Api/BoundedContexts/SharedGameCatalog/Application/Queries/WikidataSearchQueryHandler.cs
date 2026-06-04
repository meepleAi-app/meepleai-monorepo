using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Issue #1903 M6.2 — handler for <see cref="WikidataSearchQuery"/>. Resolves
/// the keyed <see cref="ICatalogProvider"/> registered under the key
/// <c>"wikidata"</c> (see <c>SharedGameCatalogServiceExtensions.RegisterCatalogSeedProviders</c>)
/// and forwards a free-text search term.
/// </summary>
internal sealed class WikidataSearchQueryHandler : IQueryHandler<WikidataSearchQuery, WikidataSearchResult>
{
    private readonly ICatalogProvider _wikidata;
    private readonly ILogger<WikidataSearchQueryHandler> _logger;

    public WikidataSearchQueryHandler(
        [FromKeyedServices("wikidata")] ICatalogProvider wikidata,
        ILogger<WikidataSearchQueryHandler> logger)
    {
        _wikidata = wikidata ?? throw new ArgumentNullException(nameof(wikidata));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WikidataSearchResult> Handle(WikidataSearchQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            return new WikidataSearchResult(
                new Dictionary<string, FieldProvenance>(StringComparer.Ordinal),
                "SearchTerm must not be empty");
        }

        var providerResult = await _wikidata
            .FetchAsync(new CatalogProviderQuery(null, null, request.SearchTerm), cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug(
            "WikidataSearchQuery: term={Term} matched {FieldCount} fields (error={Error})",
            request.SearchTerm, providerResult.Fields.Count, providerResult.ErrorMessage ?? "none");

        return new WikidataSearchResult(providerResult.Fields, providerResult.ErrorMessage);
    }
}

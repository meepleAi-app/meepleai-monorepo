using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;

/// <summary>
/// Primary catalog provider — Wikidata SPARQL endpoint.
/// Spec: 2026-06-04-admin-catalog-seed-design.md §7.1.
/// License: CC0 (all data) — no attribution required.
/// Issue #1903.
/// </summary>
internal sealed class WikidataCatalogProvider : ICatalogProvider
{
    public string Name => "wikidata";

    private const string SparqlPath = "sparql";

    // Validates Wikidata QID format (Q followed by digits) before SPARQL interpolation.
    // Anchored, no backtracking — DoS-safe. Timeout is defensive.
    private static readonly Regex WikidataQidPattern = new(
        @"^Q\d+$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    private readonly HttpClient _http;
    private readonly ILogger<WikidataCatalogProvider> _logger;

    public WikidataCatalogProvider(HttpClient http, ILogger<WikidataCatalogProvider> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CatalogProviderResult> FetchAsync(CatalogProviderQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.BggId is null && query.WikidataQid is null && string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            return CatalogProviderResult.Empty("Missing query parameters");
        }

        if (query.WikidataQid is not null && !WikidataQidPattern.IsMatch(query.WikidataQid))
        {
            return CatalogProviderResult.Empty("Invalid WikidataQid format");
        }

        var sparql = BuildSparql(query);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{SparqlPath}?query={Uri.EscapeDataString(sparql)}&format=json");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/sparql-results+json"));

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Wikidata SPARQL HTTP {Status} on query: {Sparql}", (int)resp.StatusCode, sparql);
                return CatalogProviderResult.Empty($"HTTP {(int)resp.StatusCode}");
            }

            return ParseResponse(body, query, sourceUrl: BuildSourceUrl(query));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wikidata fetch failed");
            return CatalogProviderResult.Empty(ex.GetType().Name);
        }
    }

    private static string BuildSparql(CatalogProviderQuery q)
    {
        var bind = q.BggId.HasValue
            ? $"?game wdt:P2339 \"{q.BggId.Value}\"."
            : q.WikidataQid is not null
                ? $"BIND(wd:{q.WikidataQid} AS ?game)"
                : $"?game rdfs:label \"{q.SearchTerm}\"@en.";

        return $@"
SELECT ?game ?gameLabel ?yearPublished ?designerLabel ?publisherLabel
       ?minPlayers ?maxPlayers ?playingTimeMinutes
WHERE {{
  {bind}
  OPTIONAL {{ ?game wdt:P577 ?yearPublished. }}
  OPTIONAL {{ ?game wdt:P3300 ?designer. }}
  OPTIONAL {{ ?game wdt:P123 ?publisher. }}
  OPTIONAL {{ ?game wdt:P1873 ?minPlayers. }}
  OPTIONAL {{ ?game wdt:P1872 ?maxPlayers. }}
  OPTIONAL {{ ?game wdt:P2047 ?playingTimeMinutes. }}
  SERVICE wikibase:label {{ bd:serviceParam wikibase:language ""en"". }}
}}
LIMIT 1";
    }

    private static string BuildSourceUrl(CatalogProviderQuery q)
    {
        if (q.WikidataQid is not null) return $"https://www.wikidata.org/wiki/{q.WikidataQid}";
        if (q.BggId.HasValue) return $"https://query.wikidata.org/?bggid={q.BggId}";
        return "https://query.wikidata.org/";
    }

    private static CatalogProviderResult ParseResponse(string body, CatalogProviderQuery q, string sourceUrl)
    {
        var fetchedAt = DateTime.UtcNow;
        using var doc = JsonDocument.Parse(body);
        var bindings = doc.RootElement
            .GetProperty("results")
            .GetProperty("bindings");

        if (bindings.GetArrayLength() == 0)
        {
            return CatalogProviderResult.Empty("Wikidata: entity not found");
        }

        var row = bindings[0];
        var fields = new Dictionary<string, FieldProvenance>(StringComparer.Ordinal);

        string? Get(string key) => row.TryGetProperty(key, out var el) && el.TryGetProperty("value", out var v) ? v.GetString() : null;

        var title = Get("gameLabel");
        if (!string.IsNullOrWhiteSpace(title))
        {
            fields["title"] = new FieldProvenance("wikidata", sourceUrl, "labels.en", fetchedAt, title);
        }

        var gameUri = Get("game");
        if (gameUri is not null && gameUri.StartsWith("http://www.wikidata.org/entity/", StringComparison.Ordinal))
        {
            var qid = gameUri["http://www.wikidata.org/entity/".Length..];
            fields["wikidataQid"] = new FieldProvenance("wikidata", sourceUrl, "item URI", fetchedAt, qid);
        }

        var yearRaw = Get("yearPublished");
        if (yearRaw is not null && DateTimeOffset.TryParse(yearRaw, System.Globalization.CultureInfo.InvariantCulture, out var dt))
        {
            fields["yearPublished"] = new FieldProvenance("wikidata", sourceUrl, "P577", fetchedAt, dt.Year);
        }

        var designer = Get("designerLabel");
        if (!string.IsNullOrWhiteSpace(designer))
        {
            fields["designers"] = new FieldProvenance("wikidata", sourceUrl, "P3300", fetchedAt, new List<string> { designer });
        }

        var publisher = Get("publisherLabel");
        if (!string.IsNullOrWhiteSpace(publisher))
        {
            fields["publishers"] = new FieldProvenance("wikidata", sourceUrl, "P123", fetchedAt, new List<string> { publisher });
        }

        if (int.TryParse(Get("minPlayers"), System.Globalization.CultureInfo.InvariantCulture, out var mn))
        {
            fields["minPlayers"] = new FieldProvenance("wikidata", sourceUrl, "P1873", fetchedAt, mn);
        }
        if (int.TryParse(Get("maxPlayers"), System.Globalization.CultureInfo.InvariantCulture, out var mx))
        {
            fields["maxPlayers"] = new FieldProvenance("wikidata", sourceUrl, "P1872", fetchedAt, mx);
        }
        if (int.TryParse(Get("playingTimeMinutes"), System.Globalization.CultureInfo.InvariantCulture, out var pt))
        {
            fields["playingTimeMinutes"] = new FieldProvenance("wikidata", sourceUrl, "P2047", fetchedAt, pt);
        }

        return new CatalogProviderResult(fields, body, ErrorMessage: null);
    }
}

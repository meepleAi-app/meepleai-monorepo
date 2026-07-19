using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class WikidataCatalogProviderTests
{
    private static HttpClient MakeClient(HttpStatusCode status, string body)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/sparql-results+json"),
            });
        return new HttpClient(handler.Object) { BaseAddress = new Uri("https://query.wikidata.org/") };
    }

    // Captures the outbound HttpRequestMessage so a test can assert the SPARQL shape.
    private static (HttpClient Client, Func<HttpRequestMessage?> Captured) MakeCapturingClient(string body)
    {
        HttpRequestMessage? captured = null;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/sparql-results+json"),
            });
        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://query.wikidata.org/") };
        return (client, () => captured);
    }

    [Fact]
    public void Name_Equals_Wikidata()
    {
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.OK, "{}"), NullLogger<WikidataCatalogProvider>.Instance, Mock.Of<IWikimediaRateLimiter>());
        provider.Name.Should().Be("wikidata");
    }

    [Fact]
    public async Task FetchAsync_ByBggId_MapsCoreFields()
    {
        const string body = """
        { "results": { "bindings": [{
          "game":        {"value": "http://www.wikidata.org/entity/Q98056728"},
          "gameLabel":   {"value": "Catan"},
          "yearPublished":{"value": "1995-01-01T00:00:00Z","datatype":"http://www.w3.org/2001/XMLSchema#dateTime"},
          "designerLabel":{"value": "Klaus Teuber"},
          "publisherLabel":{"value": "Kosmos"},
          "minPlayers":  {"value": "3"},
          "maxPlayers":  {"value": "4"},
          "playingTimeMinutes":{"value": "60"}
        }]}}
        """;
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.OK, body), NullLogger<WikidataCatalogProvider>.Instance, Mock.Of<IWikimediaRateLimiter>());
        var result = await provider.FetchAsync(new CatalogProviderQuery(BggId: 13, null, null), default);

        result.Success.Should().BeTrue();
        result.Fields["title"].Value.Should().Be("Catan");
        result.Fields["title"].Provider.Should().Be("wikidata");
        result.Fields["yearPublished"].Value.Should().Be(1995);
        result.Fields["designers"].Value.Should().BeOfType<List<string>>()
            .Which.Should().Contain("Klaus Teuber");
        result.Fields["minPlayers"].Value.Should().Be(3);
        result.Fields["maxPlayers"].Value.Should().Be(4);
        result.Fields["playingTimeMinutes"].Value.Should().Be(60);
        result.Fields["wikidataQid"].Value.Should().Be("Q98056728");
    }

    [Fact]
    public async Task FetchAsync_NoResults_ReturnsEmptyWithError()
    {
        const string body = """{"results":{"bindings":[]}}""";
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.OK, body), NullLogger<WikidataCatalogProvider>.Instance, Mock.Of<IWikimediaRateLimiter>());
        var result = await provider.FetchAsync(new CatalogProviderQuery(99999, null, null), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task FetchAsync_HttpError_ReturnsErrorMessage()
    {
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.InternalServerError, "boom"), NullLogger<WikidataCatalogProvider>.Instance, Mock.Of<IWikimediaRateLimiter>());
        var result = await provider.FetchAsync(new CatalogProviderQuery(13, null, null), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("HTTP");
    }

    [Fact]
    public async Task FetchAsync_AllNullInputs_ReturnsMissingParametersError()
    {
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.OK, "{}"), NullLogger<WikidataCatalogProvider>.Instance, Mock.Of<IWikimediaRateLimiter>());

        var result = await provider.FetchAsync(new CatalogProviderQuery(null, null, null), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Missing");
    }

    [Fact]
    public async Task FetchAsync_InvalidWikidataQid_ReturnsValidationError()
    {
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.OK, "{}"), NullLogger<WikidataCatalogProvider>.Instance, Mock.Of<IWikimediaRateLimiter>());

        var result = await provider.FetchAsync(new CatalogProviderQuery(null, "Q123} . ?x ?y ?z", null), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid");
    }

    [Fact]
    public async Task FetchAsync_ByValidWikidataQid_BuildsBindQuery()
    {
        // Sanity: a valid QID format does NOT trigger validation error.
        const string body = """
        { "results": { "bindings": [{
          "game":        {"value": "http://www.wikidata.org/entity/Q98056728"},
          "gameLabel":   {"value": "Catan"}
        }]}}
        """;
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.OK, body), NullLogger<WikidataCatalogProvider>.Instance, Mock.Of<IWikimediaRateLimiter>());

        var result = await provider.FetchAsync(new CatalogProviderQuery(null, "Q98056728", null), default);

        result.Success.Should().BeTrue();
        result.Fields["title"].Value.Should().Be("Catan");
        result.Fields["wikidataQid"].Value.Should().Be("Q98056728");
    }

    [Fact]
    public async Task FetchAsync_BuildsSparql_MapsP1872ToMinAndP1873ToMaxWithAggregation()
    {
        // Issue #3137: P1872 = "minimum number of players", P1873 = "maximum number of players".
        // The old query bound them SWAPPED (P1873 -> ?minPlayers) and used a flat OPTIONAL chain
        // + LIMIT 1 (cartesian product on multi-valued scalars). The corrected query maps
        // P1872 -> min / P1873 -> max and aggregates each scalar in a GROUP BY subquery.
        var (client, getCaptured) = MakeCapturingClient("""{"results":{"bindings":[]}}""");
        var provider = new WikidataCatalogProvider(client, NullLogger<WikidataCatalogProvider>.Instance, Mock.Of<IWikimediaRateLimiter>());

        await provider.FetchAsync(new CatalogProviderQuery(null, "Q17271", null), default);

        var captured = getCaptured();
        captured.Should().NotBeNull();
        var sparql = System.Text.RegularExpressions.Regex.Replace(
            Uri.UnescapeDataString(captured!.RequestUri!.Query), @"\s+", " ");

        // Minimum sourced from P1872, maximum from P1873 — no swap.
        sparql.Should().Contain("wdt:P1872 ?minRaw");
        sparql.Should().Contain("wdt:P1873 ?maxRaw");
        sparql.Should().Contain("MIN(?minRaw) AS ?minPlayers");
        sparql.Should().Contain("MAX(?maxRaw) AS ?maxPlayers");
        // Independent aggregation defuses the cartesian product (multi-valued designer/publisher/edition).
        sparql.Should().Contain("GROUP BY ?game");
        // The old swap must be gone.
        sparql.Should().NotContain("wdt:P1873 ?minPlayers");
        sparql.Should().NotContain("wdt:P1872 ?maxPlayers");
    }

    [Fact]
    public async Task FetchAsync_PlayerCounts_CarryCorrectProvenanceProperties()
    {
        // Issue #3137: minPlayers provenance must be P1872, maxPlayers must be P1873.
        const string body = """
        { "results": { "bindings": [{
          "game":       {"value": "http://www.wikidata.org/entity/Q17271"},
          "gameLabel":  {"value": "Catan"},
          "minPlayers": {"value": "3"},
          "maxPlayers": {"value": "4"}
        }]}}
        """;
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.OK, body), NullLogger<WikidataCatalogProvider>.Instance, Mock.Of<IWikimediaRateLimiter>());

        var result = await provider.FetchAsync(new CatalogProviderQuery(null, "Q17271", null), default);

        result.Fields["minPlayers"].Value.Should().Be(3);
        result.Fields["minPlayers"].SourceField.Should().Be("P1872");
        result.Fields["maxPlayers"].Value.Should().Be(4);
        result.Fields["maxPlayers"].SourceField.Should().Be("P1873");
    }

    [Fact]
    public async Task FetchAsync_BuildsSparql_MapsDesignerToP178NotP3300()
    {
        // Issue #3142: P3300 = "musical conductor" (verified live) — NOT a game designer.
        // The board-game designer is P178, the only candidate returning the correct designer
        // for Catan/Carcassonne/Pandemic.
        var (client, getCaptured) = MakeCapturingClient("""{"results":{"bindings":[]}}""");
        var provider = new WikidataCatalogProvider(client, NullLogger<WikidataCatalogProvider>.Instance, Mock.Of<IWikimediaRateLimiter>());

        await provider.FetchAsync(new CatalogProviderQuery(null, "Q17271", null), default);

        var captured = getCaptured();
        captured.Should().NotBeNull();
        var sparql = System.Text.RegularExpressions.Regex.Replace(
            Uri.UnescapeDataString(captured!.RequestUri!.Query), @"\s+", " ");

        sparql.Should().Contain("wdt:P178 ?designerRaw");
        sparql.Should().NotContain("wdt:P3300"); // the "musical conductor" property must be gone
    }

    [Fact]
    public async Task FetchAsync_Designer_CarriesP178Provenance()
    {
        // Issue #3142: designer provenance must be P178, not P3300.
        const string body = """
        { "results": { "bindings": [{
          "game":         {"value": "http://www.wikidata.org/entity/Q17271"},
          "gameLabel":    {"value": "Catan"},
          "designerLabel":{"value": "Klaus Teuber"}
        }]}}
        """;
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.OK, body), NullLogger<WikidataCatalogProvider>.Instance, Mock.Of<IWikimediaRateLimiter>());

        var result = await provider.FetchAsync(new CatalogProviderQuery(null, "Q17271", null), default);

        result.Fields["designers"].Value.Should().BeOfType<List<string>>().Which.Should().Contain("Klaus Teuber");
        result.Fields["designers"].SourceField.Should().Be("P178");
    }
}

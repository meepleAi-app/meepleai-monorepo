using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;
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

    [Fact]
    public void Name_Equals_Wikidata()
    {
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.OK, "{}"), NullLogger<WikidataCatalogProvider>.Instance);
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
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.OK, body), NullLogger<WikidataCatalogProvider>.Instance);
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
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.OK, body), NullLogger<WikidataCatalogProvider>.Instance);
        var result = await provider.FetchAsync(new CatalogProviderQuery(99999, null, null), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task FetchAsync_HttpError_ReturnsErrorMessage()
    {
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.InternalServerError, "boom"), NullLogger<WikidataCatalogProvider>.Instance);
        var result = await provider.FetchAsync(new CatalogProviderQuery(13, null, null), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("HTTP");
    }

    [Fact]
    public async Task FetchAsync_AllNullInputs_ReturnsMissingParametersError()
    {
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.OK, "{}"), NullLogger<WikidataCatalogProvider>.Instance);

        var result = await provider.FetchAsync(new CatalogProviderQuery(null, null, null), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Missing");
    }

    [Fact]
    public async Task FetchAsync_InvalidWikidataQid_ReturnsValidationError()
    {
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.OK, "{}"), NullLogger<WikidataCatalogProvider>.Instance);

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
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.OK, body), NullLogger<WikidataCatalogProvider>.Instance);

        var result = await provider.FetchAsync(new CatalogProviderQuery(null, "Q98056728", null), default);

        result.Success.Should().BeTrue();
        result.Fields["title"].Value.Should().Be("Catan");
        result.Fields["wikidataQid"].Value.Should().Be("Q98056728");
    }
}

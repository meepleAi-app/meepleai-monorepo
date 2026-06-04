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
public sealed class BggCatalogProviderTests
{
    private const string CatanXml = """
    <?xml version="1.0" encoding="utf-8"?>
    <items>
      <item type="boardgame" id="13">
        <name type="primary" value="Catan"/>
        <yearpublished value="1995"/>
        <minplayers value="3"/>
        <maxplayers value="4"/>
        <playingtime value="60"/>
        <minage value="10"/>
        <link type="boardgamedesigner" id="11" value="Klaus Teuber"/>
        <link type="boardgamepublisher" id="93" value="Kosmos"/>
        <link type="boardgamemechanic" id="2008" value="Trading"/>
        <link type="boardgamemechanic" id="2018" value="Modular Board"/>
        <description>FORBIDDEN: should never be mapped</description>
        <image>FORBIDDEN_URL</image>
      </item>
    </items>
    """;

    private static HttpClient MakeClient(HttpStatusCode status, string body)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/xml"),
            });
        return new HttpClient(handler.Object) { BaseAddress = new Uri("https://boardgamegeek.com/") };
    }

    [Fact]
    public void Name_Equals_Bgg()
    {
        var p = new BggCatalogProvider(MakeClient(HttpStatusCode.OK, "<items/>"), NullLogger<BggCatalogProvider>.Instance);
        p.Name.Should().Be("bgg");
    }

    [Fact]
    public async Task FetchAsync_MapsAllowedFieldsOnly()
    {
        var p = new BggCatalogProvider(MakeClient(HttpStatusCode.OK, CatanXml), NullLogger<BggCatalogProvider>.Instance);
        var r = await p.FetchAsync(new CatalogProviderQuery(BggId: 13, null, null), default);

        r.Success.Should().BeTrue();
        r.Fields.Should().ContainKey("title");
        r.Fields["title"].Value.Should().Be("Catan");
        r.Fields["yearPublished"].Value.Should().Be(1995);
        r.Fields["mechanics"].Value.Should().BeOfType<List<string>>().Which.Should().BeEquivalentTo("Trading", "Modular Board");
        r.Fields["designers"].Value.Should().BeOfType<List<string>>().Which.Should().Contain("Klaus Teuber");
    }

    [Fact]
    public async Task FetchAsync_NeverMapsForbiddenFields_EvenIfPresent()
    {
        var p = new BggCatalogProvider(MakeClient(HttpStatusCode.OK, CatanXml), NullLogger<BggCatalogProvider>.Instance);
        var r = await p.FetchAsync(new CatalogProviderQuery(13, null, null), default);

        // Compliance guard — forbidden fields must NEVER appear in result.
        r.Fields.Should().NotContainKey("description");
        r.Fields.Should().NotContainKey("image");
        r.Fields.Should().NotContainKey("thumbnail");
        r.Fields.Should().NotContainKey("statistics");
        r.Fields.Should().NotContainKey("comments");
    }

    [Fact]
    public async Task FetchAsync_NoBggId_ReturnsError()
    {
        var p = new BggCatalogProvider(MakeClient(HttpStatusCode.OK, ""), NullLogger<BggCatalogProvider>.Instance);
        var r = await p.FetchAsync(new CatalogProviderQuery(BggId: null, WikidataQid: null, SearchTerm: null), default);
        r.Success.Should().BeFalse();
        r.ErrorMessage.Should().Contain("BggId");
    }

    [Fact]
    public async Task FetchAsync_Http500_ReturnsErrorMessage()
    {
        var p = new BggCatalogProvider(MakeClient(HttpStatusCode.InternalServerError, "fail"), NullLogger<BggCatalogProvider>.Instance);
        var r = await p.FetchAsync(new CatalogProviderQuery(13, null, null), default);
        r.Success.Should().BeFalse();
        r.ErrorMessage.Should().Contain("HTTP");
    }
}

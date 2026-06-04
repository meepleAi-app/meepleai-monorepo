using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CatalogSeedAggregatorTests
{
    private static FieldProvenance FP(string provider, string field, object value)
        => new(provider, "http://example.test", field, DateTime.UtcNow, value);

    private static ICatalogProvider StubProvider(string name, IReadOnlyDictionary<string, FieldProvenance> fields, string? error = null)
    {
        var m = new Mock<ICatalogProvider>();
        m.SetupGet(p => p.Name).Returns(name);
        m.Setup(p => p.FetchAsync(It.IsAny<CatalogProviderQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogProviderResult(fields, "{}", error));
        return m.Object;
    }

    [Fact]
    public async Task FetchAsync_PrimaryOnly_WhenAllFieldsResolved()
    {
        var wd = StubProvider("wikidata", new Dictionary<string, FieldProvenance>
        {
            ["title"] = FP("wikidata", "labels.en", "Catan"),
            ["yearPublished"] = FP("wikidata", "P577", 1995),
        });
        var bgg = StubProvider("bgg", new Dictionary<string, FieldProvenance>());

        var agg = new CatalogSeedAggregator(wd, bgg, NullLogger<CatalogSeedAggregator>.Instance);
        var result = await agg.FetchAsync(new CatalogProviderQuery(13, null, null), default);

        result.Provenance.GetValue<string>("title").Should().Be("Catan");
        result.Provenance.GetProvider("title").Should().Be("wikidata");
        result.ProviderUsed.Should().Be("wikidata");
    }

    [Fact]
    public async Task FetchAsync_PrimaryAndFallback_WhenWikidataMissing()
    {
        var wd = StubProvider("wikidata", new Dictionary<string, FieldProvenance>
        {
            ["title"] = FP("wikidata", "labels.en", "Catan"),
        });
        var bgg = StubProvider("bgg", new Dictionary<string, FieldProvenance>
        {
            ["mechanics"] = FP("bgg", "boardgamemechanic", new List<string> { "Trading" }),
            ["title"] = FP("bgg", "name", "Settlers of Catan"),
        });

        var agg = new CatalogSeedAggregator(wd, bgg, NullLogger<CatalogSeedAggregator>.Instance);
        var result = await agg.FetchAsync(new CatalogProviderQuery(13, null, null), default);

        result.Provenance.GetValue<string>("title").Should().Be("Catan");          // wd wins
        result.Provenance.GetProvider("mechanics").Should().Be("bgg");             // bgg fills missing
        result.ProviderUsed.Should().Be("wikidata+bgg");
    }

    [Fact]
    public async Task FetchAsync_FallbackOnly_WhenWikidataFailed()
    {
        var wd = StubProvider("wikidata", new Dictionary<string, FieldProvenance>(), error: "not found");
        var bgg = StubProvider("bgg", new Dictionary<string, FieldProvenance>
        {
            ["title"] = FP("bgg", "name", "MyIndie"),
        });

        var agg = new CatalogSeedAggregator(wd, bgg, NullLogger<CatalogSeedAggregator>.Instance);
        var result = await agg.FetchAsync(new CatalogProviderQuery(50000, null, null), default);

        result.Provenance.GetValue<string>("title").Should().Be("MyIndie");
        result.ProviderUsed.Should().Be("bgg");
    }

    [Fact]
    public async Task FetchAsync_BothFailed_ReturnsEmptyProvenance()
    {
        var wd = StubProvider("wikidata", new Dictionary<string, FieldProvenance>(), error: "fail-wd");
        var bgg = StubProvider("bgg", new Dictionary<string, FieldProvenance>(), error: "fail-bgg");

        var agg = new CatalogSeedAggregator(wd, bgg, NullLogger<CatalogSeedAggregator>.Instance);
        var result = await agg.FetchAsync(new CatalogProviderQuery(99999, null, null), default);

        result.Provenance.Fields.Should().BeEmpty();
        result.ProviderUsed.Should().Be("none");
    }

    [Fact]
    public void Constructor_WrongProviderName_ThrowsArgumentException()
    {
        var wrongPrimary = StubProvider("bgg", new Dictionary<string, FieldProvenance>());
        var bgg = StubProvider("bgg", new Dictionary<string, FieldProvenance>());

        var act = () => new CatalogSeedAggregator(wrongPrimary, bgg, NullLogger<CatalogSeedAggregator>.Instance);
        act.Should().Throw<ArgumentException>().WithMessage("*wikidata*");
    }
}

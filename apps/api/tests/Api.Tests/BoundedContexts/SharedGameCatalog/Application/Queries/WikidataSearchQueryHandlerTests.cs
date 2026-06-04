using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Unit tests for <see cref="WikidataSearchQueryHandler"/> (Issue #1903 M6.2).
/// Validates the CQRS-compliant proxy wrapper around the keyed Wikidata
/// <c>ICatalogProvider</c> consumed by the admin catalog-seed UI picker.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class WikidataSearchQueryHandlerTests
{
    private readonly Mock<ICatalogProvider> _wikidata = new();

    private WikidataSearchQueryHandler CreateHandler() =>
        new(_wikidata.Object, NullLogger<WikidataSearchQueryHandler>.Instance);

    [Fact]
    public async Task Handle_NullRequest_Throws()
    {
        var act = async () => await CreateHandler().Handle(null!, default);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_EmptySearchTerm_ReturnsErrorResultWithoutCallingProvider()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new WikidataSearchQuery(""), default);

        result.Fields.Should().BeEmpty();
        result.ErrorMessage.Should().Be("SearchTerm must not be empty");
        _wikidata.Verify(p => p.FetchAsync(
            It.IsAny<CatalogProviderQuery>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhitespaceSearchTerm_ReturnsErrorResultWithoutCallingProvider()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new WikidataSearchQuery("   "), default);

        result.Fields.Should().BeEmpty();
        result.ErrorMessage.Should().Be("SearchTerm must not be empty");
        _wikidata.Verify(p => p.FetchAsync(
            It.IsAny<CatalogProviderQuery>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidSearchTerm_ForwardsToProviderAndProjectsResult()
    {
        var fields = new Dictionary<string, FieldProvenance>(StringComparer.Ordinal)
        {
            ["title"] = new FieldProvenance(
                Provider: "wikidata",
                SourceUrl: "https://www.wikidata.org/wiki/Q123",
                SourceField: "labels.en",
                FetchedAt: DateTime.UtcNow,
                Value: "Catan"),
        };
        _wikidata
            .Setup(p => p.FetchAsync(It.IsAny<CatalogProviderQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogProviderResult(fields, RawPayloadJson: null, ErrorMessage: null));

        var result = await CreateHandler().Handle(new WikidataSearchQuery("Catan"), default);

        result.ErrorMessage.Should().BeNull();
        result.Fields.Should().HaveCount(1);
        result.Fields["title"].Value.Should().Be("Catan");

        // Provider invoked with the search term forwarded into the SearchTerm slot,
        // and BggId / WikidataQid left null (this is a search proxy, not a lookup).
        _wikidata.Verify(p => p.FetchAsync(
            It.Is<CatalogProviderQuery>(q =>
                q.BggId == null && q.WikidataQid == null && q.SearchTerm == "Catan"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProviderReturnsError_PropagatesErrorMessage()
    {
        _wikidata
            .Setup(p => p.FetchAsync(It.IsAny<CatalogProviderQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CatalogProviderResult.Empty("SPARQL timeout"));

        var result = await CreateHandler().Handle(new WikidataSearchQuery("anything"), default);

        result.Fields.Should().BeEmpty();
        result.ErrorMessage.Should().Be("SPARQL timeout");
    }
}

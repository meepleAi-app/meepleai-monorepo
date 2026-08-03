using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Domain.Covers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Epic #3470 Slice 1d-a — the cover attribution footer must follow the WINNING
/// source, not be emitted unconditionally. A game can carry Wikidata attribution
/// columns while a PDF/BGG cover actually wins; crediting Wikidata over a
/// non-Wikidata image is the legal-correctness bug this maps against.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CoverAttributionTests
{
    private static SharedGameEntity EntityWithWikidata() => new()
    {
        WikidataCoverLicense = "CC BY-SA 4.0",
        WikidataCoverAttribution = "<a href=\"x\">Artist</a>",
        WikidataCoverSourceUrl = "https://www.wikidata.org/wiki/Q1",
    };

    [Fact]
    public void ForWinningSource_Wikidata_ReturnsStrippedWikidataTriple()
    {
        var (license, attribution, sourceUrl) = CoverAttribution.ForWinningSource(CoverKind.Wikidata, EntityWithWikidata());

        license.Should().Be("CC BY-SA 4.0");
        attribution.Should().Be("Artist", "the HTML must be stripped (DEC-G6-1)");
        sourceUrl.Should().Be("https://www.wikidata.org/wiki/Q1");
    }

    [Fact]
    public void ForWinningSource_PdfWins_SuppressesWikidataAttribution()
    {
        // The game HAS Wikidata columns, but PDF is the winning cover — the footer
        // must NOT credit Wikidata over the PDF-derived image.
        var (license, attribution, sourceUrl) = CoverAttribution.ForWinningSource(CoverKind.Pdf, EntityWithWikidata());

        license.Should().BeNull();
        attribution.Should().BeNull();
        sourceUrl.Should().BeNull();
    }

    [Fact]
    public void ForWinningSource_BggWins_SuppressesWikidataAttribution()
    {
        var (license, attribution, sourceUrl) = CoverAttribution.ForWinningSource(CoverKind.Bgg, EntityWithWikidata());

        license.Should().BeNull();
        attribution.Should().BeNull();
        sourceUrl.Should().BeNull();
    }

    [Fact]
    public void ForWinningSource_NoWinner_SuppressesAttribution()
    {
        var (license, attribution, sourceUrl) = CoverAttribution.ForWinningSource(null, EntityWithWikidata());

        license.Should().BeNull();
        attribution.Should().BeNull();
        sourceUrl.Should().BeNull();
    }

    [Fact]
    public void ForWinningSource_WikidataWinsButNoColumns_ReturnsNulls()
    {
        var (license, attribution, sourceUrl) = CoverAttribution.ForWinningSource(CoverKind.Wikidata, new SharedGameEntity());

        license.Should().BeNull();
        attribution.Should().BeNull();
        sourceUrl.Should().BeNull();
    }
}

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

    // ----- Epic #3470 Slice 3b: Manual cover attribution (copyright-critical) -----

    private static SharedGameEntity EntityWithManual() => new()
    {
        ManualCoverLicense = "CC-BY-4.0",
        ManualCoverAttribution = "<b>Jane Artist</b>",
        ManualCoverSourceUrl = "https://commons.example.org/img",
    };

    [Fact]
    public void ForWinningSource_ManualWins_ReturnsStrippedManualTriple()
    {
        // A CC-BY/CC-BY-SA manual cover MUST surface its attested license + attribution,
        // else the whitelist-admitted image renders without the legally-required credit.
        var (license, attribution, sourceUrl) = CoverAttribution.ForWinningSource(CoverKind.Manual, EntityWithManual());

        license.Should().Be("CC-BY-4.0");
        attribution.Should().Be("Jane Artist", "the attested attribution is HTML-stripped like Wikidata");
        sourceUrl.Should().Be("https://commons.example.org/img");
    }

    [Fact]
    public void ForWinningSource_ManualWinsButNoColumns_ReturnsNulls()
    {
        var (license, attribution, sourceUrl) = CoverAttribution.ForWinningSource(CoverKind.Manual, new SharedGameEntity());

        license.Should().BeNull();
        attribution.Should().BeNull();
        sourceUrl.Should().BeNull();
    }

    [Fact]
    public void ForWinningSource_ManualWins_DoesNotLeakWikidata()
    {
        // Entity carries BOTH Wikidata and Manual columns; Manual is the winner → only the
        // Manual triple surfaces (never a Wikidata credit over a Manual image).
        var entity = new SharedGameEntity
        {
            WikidataCoverLicense = "CC BY-SA 4.0",
            WikidataCoverAttribution = "Wiki Author",
            WikidataCoverSourceUrl = "https://www.wikidata.org/wiki/Q1",
            ManualCoverLicense = "CC0",
            ManualCoverAttribution = "Manual Author",
            ManualCoverSourceUrl = "https://commons.example.org/m",
        };

        var (license, attribution, sourceUrl) = CoverAttribution.ForWinningSource(CoverKind.Manual, entity);

        license.Should().Be("CC0");
        attribution.Should().Be("Manual Author");
        sourceUrl.Should().Be("https://commons.example.org/m");
    }
}

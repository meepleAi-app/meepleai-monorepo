using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.Catalog;

/// <summary>
/// Regression guard for #2679: the non-indexable rulebooks (corrupted masters and scan-only PDFs
/// with OCR disabled on staging) were removed from the seed manifests (PR #2680),
/// while the games themselves stay in the catalog. This test locks that removal in across all
/// three profiles so a future re-bake (<c>make seed-index</c>, #2516) cannot silently
/// re-introduce a <c>pdfBlobKey</c>/<c>pdfSha256</c>/<c>pdfVersion</c> for these games.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class SeedManifestNonIndexableGamesTests
{
    // (Title, BggId) of the games whose rulebook PDF was stripped per #2679.
    // Corrupted masters: Exploding Kittens, Betrayal at House on the Hill.
    // Scan-only (OCR disabled by design): Lost Ruins of Arnak, Hive Pocket, Guillotine.
    // Gloomhaven left this list with #4470: its master was a scan without text
    // (gloomhaven_rulebook.pdf), and it now carries an Italian rulebook that has one.
    private static readonly (string Title, int BggId)[] NonIndexable =
    {
        ("Exploding Kittens", 172225),
        ("Betrayal at House on the Hill", 10547),
        ("Lost Ruins of Arnak", 312484),
        ("Hive Pocket", 154597),
        ("Guillotine", 116),
    };

    /// <summary>
    /// #4470: Gloomhaven (174430) is seeded from the Italian translation of the rulebook, which has a
    /// text layer, and never again from <c>gloomhaven_rulebook.pdf</c>: that file is 74 pages of images
    /// without a single character of text, so indexing it gives the agent nothing.
    /// </summary>
    [Theory]
    [InlineData(SeedProfile.Dev)]
    [InlineData(SeedProfile.Staging)]
    [InlineData(SeedProfile.Prod)]
    public void Gloomhaven_IsSeededFromTheItalianRulebookWithText_NotFromTheScan(SeedProfile profile)
    {
        var manifest = CatalogSeeder.LoadManifest(profile);

        var gloomhaven = manifest.Catalog.Games
            .SingleOrDefault(g => string.Equals(g.Title, "Gloomhaven", StringComparison.OrdinalIgnoreCase));

        gloomhaven.Should().NotBeNull();
        gloomhaven!.BggId.Should().Be(174430);
        gloomhaven.PdfBlobKey.Should().Be("rulebooks/v1/gloomhaven-it_rulebook.pdf",
            $"the {profile} manifest must seed the rulebook that has text, not the scan");
        gloomhaven.PdfSha256.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(SeedProfile.Dev)]
    [InlineData(SeedProfile.Staging)]
    [InlineData(SeedProfile.Prod)]
    public void NonIndexableGames_KeptInCatalog_ButCarryNoRulebookPdf(SeedProfile profile)
    {
        var manifest = CatalogSeeder.LoadManifest(profile);

        foreach (var (title, bggId) in NonIndexable)
        {
            var game = manifest.Catalog.Games
                .SingleOrDefault(g => string.Equals(g.Title, title, StringComparison.OrdinalIgnoreCase));

            game.Should().NotBeNull(
                $"#2679 keeps '{title}' in the {profile} catalog (only its rulebook was removed)");
            game!.BggId.Should().Be(bggId,
                $"'{title}' should resolve to the expected game (guards against a same-named entry)");
            game.PdfBlobKey.Should().BeNullOrWhiteSpace(
                $"#2679 removed the non-indexable rulebook of '{title}' from the {profile} manifest");
            game.PdfSha256.Should().BeNullOrWhiteSpace(
                $"#2679 removed the rulebook hash of '{title}' from the {profile} manifest");
            game.PdfVersion.Should().BeNullOrWhiteSpace(
                $"#2679 removed the rulebook version of '{title}' from the {profile} manifest");
        }
    }

    [Fact]
    public void JawsOfTheLion_KeepsItsRulebook_NegativeControl()
    {
        // 'Gloomhaven: Jaws of the Lion' (bggId 291457) is a DIFFERENT game that legitimately
        // keeps its rulebook. This negative control ensures the guard above cannot be satisfied
        // by an over-broad strip that also drops a valid rulebook.
        var manifest = CatalogSeeder.LoadManifest(SeedProfile.Staging);

        var jaws = manifest.Catalog.Games
            .SingleOrDefault(g => string.Equals(g.Title, "Gloomhaven: Jaws of the Lion", StringComparison.OrdinalIgnoreCase));

        jaws.Should().NotBeNull();
        jaws!.BggId.Should().Be(291457);
        jaws.PdfBlobKey.Should().NotBeNullOrWhiteSpace(
            "Jaws of the Lion keeps its rulebook — the #2679 strip must be scoped to the base Gloomhaven (174430)");
    }
}

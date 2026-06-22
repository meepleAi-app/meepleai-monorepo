using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.Catalog;

[Trait("Category", TestCategories.Unit)]
public sealed class CatalogSeederTests
{
    [Fact]
    public void LoadManifest_DevProfile_ReturnsValidManifest()
    {
        var manifest = CatalogSeeder.LoadManifest(SeedProfile.Dev);

        manifest.Should().NotBeNull();
        manifest.Profile.Should().Be("dev");
        manifest.Catalog.Games.Should().HaveCountGreaterThanOrEqualTo(3);
        manifest.Catalog.Games.Should().Contain(g => g.Title == "Catan");
    }

    [Fact]
    public void LoadManifest_DevProfile_ContainsExpectedGames()
    {
        var manifest = CatalogSeeder.LoadManifest(SeedProfile.Dev);

        var titles = manifest.Catalog.Games.Select(g => g.Title).ToList();
        titles.Should().Contain("Catan");
        titles.Should().Contain("Wingspan");
        titles.Should().Contain("Azul");
    }

    [Fact]
    public void LoadManifest_DevProfile_GamesHaveValidBggIds()
    {
        var manifest = CatalogSeeder.LoadManifest(SeedProfile.Dev);

        // BggId is nullable: libro games (e.g. Nanolith, PR #971) are not catalogued
        // on BoardGameGeek by design. Standard published board games MUST have a
        // positive BggId; libro/narrative games may have null.
        manifest.Catalog.Games.Should().AllSatisfy(g =>
        {
            g.Title.Should().NotBeNullOrWhiteSpace();
            var isGamebook = g.Categories?.Contains("Gamebook") ?? false;
            if (isGamebook)
            {
                // Libro game: BggId may be null. No assertion on BggId value.
                return;
            }
            g.BggId.Should().BeGreaterThan(0,
                $"non-gamebook game '{g.Title}' should have a valid BggId");
        });
    }

    // Issue #2123 — BGG ToS compliance: "fallback image URL" contract retired.
    // Covers are now resolved at runtime via CoverUrlResolver from R2 assets
    // (PDF / Wikidata) or render a deterministic placeholder. No fallback
    // URL is carried in the manifest; the LoadManifest_DevProfile_GamesHaveFallbackImages
    // test has been removed because the contract it asserted no longer exists.

    [Fact]
    public void LoadManifest_DevProfile_HasEnhancedGames()
    {
        // Issue #2123: the legacy `bggEnhanced` flag is removed.
        // "Enhanced" semantic is now derived from presence of Description.
        var manifest = CatalogSeeder.LoadManifest(SeedProfile.Dev);

        manifest.Catalog.Games.Should().Contain(g => !string.IsNullOrWhiteSpace(g.Description),
            "at least some games should carry enhanced metadata (description, categories, etc.)");
    }

    [Fact]
    public void LoadManifest_DevProfile_EnhancedGamesHaveCategories()
    {
        var manifest = CatalogSeeder.LoadManifest(SeedProfile.Dev);

        var enhancedGames = manifest.Catalog.Games
            .Where(g => !string.IsNullOrWhiteSpace(g.Description))
            .ToList();
        enhancedGames.Should().NotBeEmpty();
        enhancedGames.Should().Contain(g => g.Categories != null && g.Categories.Count > 0,
            "enhanced games should have categories");
    }

    [Fact]
    public void LoadManifest_DevProfile_EnhancedGamesHaveDescriptions()
    {
        var manifest = CatalogSeeder.LoadManifest(SeedProfile.Dev);

        var enhancedGames = manifest.Catalog.Games
            .Where(g => !string.IsNullOrWhiteSpace(g.Description))
            .ToList();
        enhancedGames.Should().AllSatisfy(g =>
        {
            g.Description.Should().NotBeNullOrWhiteSpace(
                $"enhanced game '{g.Title}' should have a description");
        });
    }

    [Fact]
    public void LoadManifest_NoneProfile_ThrowsFileNotFound()
    {
        var act = () => CatalogSeeder.LoadManifest(SeedProfile.None);

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void LoadManifest_DevProfile_HasDefaultAgent()
    {
        var manifest = CatalogSeeder.LoadManifest(SeedProfile.Dev);

        manifest.Catalog.DefaultAgent.Should().NotBeNull();
        manifest.Catalog.DefaultAgent!.Name.Should().NotBeNullOrWhiteSpace();
        manifest.Catalog.DefaultAgent.Model.Should().NotBeNullOrWhiteSpace();
    }
}

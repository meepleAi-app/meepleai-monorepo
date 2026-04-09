using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.Catalog;

[Trait("Category", TestCategories.Unit)]
public sealed class CatalogSeederManifestOverrideTests
{
    [Fact]
    public void LoadManifest_WithCiOverride_LoadsCiManifest()
    {
        var manifest = CatalogSeeder.LoadManifest(SeedProfile.Dev, manifestName: "ci");

        manifest.Should().NotBeNull();
        manifest.Catalog.Games.Should().HaveCount(3);
        manifest.Catalog.Games.Select(g => g.Title).Should().BeEquivalentTo(
            new[] { "Love Letter", "Patchwork", "Jaipur" });
    }

    [Fact]
    public void LoadManifest_WithCiOverride_AllGamesHavePdfBlobKey()
    {
        var manifest = CatalogSeeder.LoadManifest(SeedProfile.Dev, manifestName: "ci");

        manifest.Catalog.Games.Should().AllSatisfy(g =>
            g.PdfBlobKey.Should().NotBeNullOrWhiteSpace(
                $"ci.yml game '{g.Title}' must have a pdfBlobKey for e2e tests"));
    }

    [Fact]
    public void LoadManifest_WithMissingOverride_Throws()
    {
        var act = () => CatalogSeeder.LoadManifest(SeedProfile.Dev, manifestName: "nonexistent");

        act.Should().Throw<FileNotFoundException>()
            .WithMessage("*nonexistent*");
    }

    [Fact]
    public void LoadManifest_WithoutOverride_StillLoadsByProfile()
    {
        // Regression: ensure the existing call signature still works
        var manifest = CatalogSeeder.LoadManifest(SeedProfile.Dev);

        manifest.Should().NotBeNull();
        manifest.Profile.Should().Be("dev");
    }
}

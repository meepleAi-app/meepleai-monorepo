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

        manifest.Catalog.Games.Should().AllSatisfy(g =>
        {
            g.BggId.Should().BeGreaterThan(0, $"game '{g.Title}' should have a valid BggId");
            g.Title.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public void LoadManifest_DevProfile_GamesHaveFallbackImages()
    {
        var manifest = CatalogSeeder.LoadManifest(SeedProfile.Dev);

        manifest.Catalog.Games.Should().AllSatisfy(g =>
        {
            g.FallbackImageUrl.Should().NotBeNullOrWhiteSpace(
                $"game '{g.Title}' should have a fallback image URL");
            g.FallbackThumbnailUrl.Should().NotBeNullOrWhiteSpace(
                $"game '{g.Title}' should have a fallback thumbnail URL");
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

using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

public sealed class CatalogSeedLayerTests
{
    [Fact]
    public void Name_IsCatalog()
    {
        var layer = new CatalogSeedLayer();
        Assert.Equal("Catalog", layer.Name);
    }

    [Fact]
    public void MinimumProfile_IsStaging()
    {
        var layer = new CatalogSeedLayer();
        Assert.Equal(SeedProfile.Staging, layer.MinimumProfile);
    }
}

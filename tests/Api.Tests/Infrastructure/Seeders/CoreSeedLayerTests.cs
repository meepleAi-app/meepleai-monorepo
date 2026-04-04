using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Core;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

public sealed class CoreSeedLayerTests
{
    [Fact]
    public void Name_IsCore()
    {
        var layer = new CoreSeedLayer();
        Assert.Equal("Core", layer.Name);
    }

    [Fact]
    public void MinimumProfile_IsProd()
    {
        var layer = new CoreSeedLayer();
        Assert.Equal(SeedProfile.Prod, layer.MinimumProfile);
    }
}

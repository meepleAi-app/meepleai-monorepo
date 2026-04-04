using Api.Infrastructure.Seeders;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

public sealed class SeedOrchestratorTests
{
    [Fact]
    public void GetProfile_ReadsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seeding:Profile"] = "Staging"
            })
            .Build();

        var profile = SeedOrchestrator.ResolveProfile(config);

        Assert.Equal(SeedProfile.Staging, profile);
    }

    [Fact]
    public void GetProfile_DefaultsToDev_WhenNotSet()
    {
        var config = new ConfigurationBuilder().Build();

        var profile = SeedOrchestrator.ResolveProfile(config);

        Assert.Equal(SeedProfile.Dev, profile);
    }

    [Fact]
    public void GetProfile_ReadsFromEnvironmentVariable()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SEED_PROFILE"] = "Prod"
            })
            .Build();

        var profile = SeedOrchestrator.ResolveProfile(config);

        Assert.Equal(SeedProfile.Prod, profile);
    }

    [Fact]
    public void GetProfile_ConfigTakesPrecedenceOverEnvVar()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seeding:Profile"] = "Staging",
                ["SEED_PROFILE"] = "Prod"
            })
            .Build();

        var profile = SeedOrchestrator.ResolveProfile(config);

        Assert.Equal(SeedProfile.Staging, profile);
    }

    [Fact]
    public void FilterLayers_ExcludesLayersAboveProfile()
    {
        var coreMock = CreateMockLayer("Core", SeedProfile.Prod);
        var catalogMock = CreateMockLayer("Catalog", SeedProfile.Staging);
        var livedInMock = CreateMockLayer("LivedIn", SeedProfile.Dev);

        var layers = new[] { coreMock, catalogMock, livedInMock };

        var filtered = SeedOrchestrator.FilterLayers(layers, SeedProfile.Staging);

        Assert.Equal(2, filtered.Count);
        Assert.Equal("Core", filtered[0].Name);
        Assert.Equal("Catalog", filtered[1].Name);
    }

    [Fact]
    public void FilterLayers_NoneProfile_ReturnsEmpty()
    {
        var coreMock = CreateMockLayer("Core", SeedProfile.Prod);
        var layers = new[] { coreMock };

        var filtered = SeedOrchestrator.FilterLayers(layers, SeedProfile.None);

        Assert.Empty(filtered);
    }

    [Fact]
    public void FilterLayers_DevProfile_IncludesAllLayers()
    {
        var coreMock = CreateMockLayer("Core", SeedProfile.Prod);
        var catalogMock = CreateMockLayer("Catalog", SeedProfile.Staging);
        var livedInMock = CreateMockLayer("LivedIn", SeedProfile.Dev);

        var layers = new[] { coreMock, catalogMock, livedInMock };

        var filtered = SeedOrchestrator.FilterLayers(layers, SeedProfile.Dev);

        Assert.Equal(3, filtered.Count);
    }

    [Fact]
    public void FilterLayers_ProdProfile_IncludesOnlyCore()
    {
        var coreMock = CreateMockLayer("Core", SeedProfile.Prod);
        var catalogMock = CreateMockLayer("Catalog", SeedProfile.Staging);
        var livedInMock = CreateMockLayer("LivedIn", SeedProfile.Dev);

        var layers = new[] { coreMock, catalogMock, livedInMock };

        var filtered = SeedOrchestrator.FilterLayers(layers, SeedProfile.Prod);

        Assert.Single(filtered);
        Assert.Equal("Core", filtered[0].Name);
    }

    private static ISeedLayer CreateMockLayer(string name, SeedProfile minProfile)
    {
        var layer = Substitute.For<ISeedLayer>();
        layer.Name.Returns(name);
        layer.MinimumProfile.Returns(minProfile);
        return layer;
    }
}

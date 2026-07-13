using Api.Infrastructure.Seeders;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

/// <summary>
/// Tests for SeedOrchestrator static methods: ResolveProfile, FilterLayers.
/// Env-var tests serialized to avoid parallel interference.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Collection("EnvironmentVariableTests")]
public sealed class SeedOrchestratorTests
{
    [Theory]
    [InlineData("None", SeedProfile.None)]
    [InlineData("Prod", SeedProfile.Prod)]
    [InlineData("Staging", SeedProfile.Staging)]
    [InlineData("Dev", SeedProfile.Dev)]
    [InlineData("dev", SeedProfile.Dev)]
    [InlineData("PROD", SeedProfile.Prod)]
    public void ResolveProfile_FromEnvironment_ParsesCorrectly(string envValue, SeedProfile expected)
    {
        Environment.SetEnvironmentVariable("SEED_PROFILE", envValue);
        try
        {
            var result = SeedOrchestrator.ResolveProfile(null);
            result.Should().Be(expected);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEED_PROFILE", null);
        }
    }

    [Fact]
    public void ResolveProfile_FromConfig_WhenNoEnvVar()
    {
        Environment.SetEnvironmentVariable("SEED_PROFILE", null);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Seeding:Profile"] = "Staging" })
            .Build();

        var result = SeedOrchestrator.ResolveProfile(config);
        result.Should().Be(SeedProfile.Staging);
    }

    [Theory]
    [Trait("Category", TestCategories.Unit)]
    [InlineData("Production", SeedProfile.Prod)]
    [InlineData("production", SeedProfile.Prod)]
    [InlineData("  Staging  ", SeedProfile.Staging)]
    [InlineData("Development", SeedProfile.Dev)]
    [InlineData("Testing", SeedProfile.None)]
    [InlineData("CI", SeedProfile.None)]
    [InlineData("", SeedProfile.None)]
    [InlineData("garbage", SeedProfile.None)]
    public void ResolveProfile_DerivesFromAspNetCoreEnvironment_WhenNoOverride(string? environmentName, SeedProfile expected)
    {
        Environment.SetEnvironmentVariable("SEED_PROFILE", null);
        try
        {
            var result = SeedOrchestrator.ResolveProfile(configuration: null, environmentName: environmentName);
            result.Should().Be(expected);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEED_PROFILE", null);
        }
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void ResolveProfile_EnvironmentVariable_TakesPrecedenceOverDerive()
    {
        Environment.SetEnvironmentVariable("SEED_PROFILE", "Staging");
        try
        {
            var result = SeedOrchestrator.ResolveProfile(configuration: null, environmentName: "Production");
            result.Should().Be(SeedProfile.Staging); // explicit override beats derived Prod
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEED_PROFILE", null);
        }
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void ResolveProfile_Config_TakesPrecedenceOverDerive_WhenNoEnvVar()
    {
        Environment.SetEnvironmentVariable("SEED_PROFILE", null);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Seeding:Profile"] = "Staging" })
            .Build();

        var result = SeedOrchestrator.ResolveProfile(config, environmentName: "Production");
        result.Should().Be(SeedProfile.Staging); // config override beats derived Prod
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void ResolveProfile_InvalidEnvVar_FallsThroughToDerive()
    {
        Environment.SetEnvironmentVariable("SEED_PROFILE", "Prdo"); // typo → invalid enum
        try
        {
            var result = SeedOrchestrator.ResolveProfile(configuration: null, environmentName: "Production");
            result.Should().Be(SeedProfile.Prod); // invalid value ignored, derives from env
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEED_PROFILE", null);
        }
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void ResolveProfile_InvalidConfig_FallsThroughToDerive()
    {
        Environment.SetEnvironmentVariable("SEED_PROFILE", null);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Seeding:Profile"] = "Prdo" }) // invalid enum
            .Build();

        var result = SeedOrchestrator.ResolveProfile(config, environmentName: "Production");
        result.Should().Be(SeedProfile.Prod); // invalid config value ignored, derives from env
    }

    [Fact]
    public void FilterLayers_ReturnsOnlyMatchingLayers()
    {
        var core = MockLayer("Core", SeedProfile.Prod);
        var catalog = MockLayer("Catalog", SeedProfile.Staging);
        var livedIn = MockLayer("LivedIn", SeedProfile.Dev);
        var layers = new[] { core, catalog, livedIn };

        var prodLayers = SeedOrchestrator.FilterLayers(layers, SeedProfile.Prod);
        var stagingLayers = SeedOrchestrator.FilterLayers(layers, SeedProfile.Staging);
        var devLayers = SeedOrchestrator.FilterLayers(layers, SeedProfile.Dev);

        prodLayers.Should().HaveCount(1).And.Contain(core);
        stagingLayers.Should().HaveCount(2);
        devLayers.Should().HaveCount(3);
    }

    [Fact]
    public void FilterLayers_OrdersByMinimumProfile()
    {
        var livedIn = MockLayer("LivedIn", SeedProfile.Dev);
        var core = MockLayer("Core", SeedProfile.Prod);
        var layers = new[] { livedIn, core }; // Reverse order

        var result = SeedOrchestrator.FilterLayers(layers, SeedProfile.Dev);

        result[0].Name.Should().Be("Core");
        result[1].Name.Should().Be("LivedIn");
    }

    [Fact]
    public void FilterLayers_ReturnsReadOnlyList()
    {
        var layers = new[] { MockLayer("Core", SeedProfile.Prod) };
        var result = SeedOrchestrator.FilterLayers(layers, SeedProfile.Dev);

        result.Should().BeAssignableTo<IReadOnlyList<ISeedLayer>>();
    }

    [Fact]
    public void FilterLayers_NoneProfile_ReturnsEmpty()
    {
        var layers = new[] { MockLayer("Core", SeedProfile.Prod) };
        var result = SeedOrchestrator.FilterLayers(layers, SeedProfile.None);

        result.Should().BeEmpty();
    }

    private static ISeedLayer MockLayer(string name, SeedProfile minProfile)
    {
        var mock = new Mock<ISeedLayer>();
        mock.Setup(l => l.Name).Returns(name);
        mock.Setup(l => l.MinimumProfile).Returns(minProfile);
        return mock.Object;
    }
}

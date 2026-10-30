using Api.SharedKernel.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.SharedKernel.Services;

/// <summary>
/// #4774: the order in which the public front-end URL is looked up, and what counts as a value.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class PublicBaseUrlProviderTests
{
    private const string Deployed = PublicBaseUrlTestSupport.DeployedEnvironment;

    private static PublicBaseUrlResolution Resolve(
        Dictionary<string, string?> values, string environmentName = Deployed) =>
        PublicBaseUrlTestSupport.ProviderFor(values, environmentName).Resolve();

    [Fact]
    public void TheFrontendKeyIsTheFirstSource()
    {
        var resolution = Resolve(new()
        {
            ["Frontend:BaseUrl"] = "https://frontend.example.test",
            ["App:BaseUrl"] = "https://legacy.example.test",
            ["FRONTEND_BASE_URL"] = "https://variable.example.test",
        });

        resolution.Url.Should().Be("https://frontend.example.test");
        resolution.Source.Should().Be("Frontend:BaseUrl");
    }

    [Fact]
    public void TheLegacyAppKeyComesSecond_SoAnExistingDeploymentKeepsWorking()
    {
        var resolution = Resolve(new()
        {
            ["App:BaseUrl"] = "https://legacy.example.test",
            ["FRONTEND_BASE_URL"] = "https://variable.example.test",
        });

        resolution.Url.Should().Be("https://legacy.example.test");
        resolution.Source.Should().Be("App:BaseUrl");
    }

    [Fact]
    public void TheEnvironmentVariableComesThird()
    {
        var resolution = Resolve(new() { ["FRONTEND_BASE_URL"] = "https://variable.example.test" });

        resolution.Url.Should().Be("https://variable.example.test");
        resolution.Source.Should().Be("FRONTEND_BASE_URL");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    public void ABlankValueIsNotAValue_TheSearchGoesOn(string? blank)
    {
        // `${VAR:-}` in a compose file hands the container a PRESENT, EMPTY variable.
        var resolution = Resolve(new()
        {
            ["Frontend:BaseUrl"] = blank,
            ["App:BaseUrl"] = blank,
            ["FRONTEND_BASE_URL"] = "https://variable.example.test",
        });

        resolution.Url.Should().Be("https://variable.example.test");
        resolution.Source.Should().Be("FRONTEND_BASE_URL");
    }

    [Theory]
    [InlineData("https://front.example.test/", "https://front.example.test")]
    [InlineData("https://front.example.test///", "https://front.example.test")]
    [InlineData("  https://front.example.test  ", "https://front.example.test")]
    [InlineData("https://front.example.test/app/", "https://front.example.test/app")]
    public void TrailingSlashesAndSurroundingSpacesAreTakenOff(string configured, string expected)
    {
        Resolve(new() { ["Frontend:BaseUrl"] = configured }).Url.Should().Be(expected);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void ALocalEnvironmentWithNothingConfigured_GetsLocalhost(string environment)
    {
        var resolution = Resolve(new(), environment);

        resolution.Url.Should().Be("http://localhost:3000");
        resolution.Source.Should().Be(PublicBaseUrlProvider.LocalDefaultSource);
        resolution.LocalEnvironment.Should().BeTrue();
    }

    [Fact]
    public void ALocalEnvironment_StillPrefersAConfiguredValueToLocalhost()
    {
        // The dev machine of the simulation: FRONTEND_BASE_URL says 13000, and that is what a link must say.
        var resolution = Resolve(new() { ["FRONTEND_BASE_URL"] = "http://localhost:13000" }, "Development");

        resolution.Url.Should().Be("http://localhost:13000");
        resolution.Source.Should().Be("FRONTEND_BASE_URL");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("CI")]
    public void ADeployedEnvironmentWithNothingConfigured_HasNoUrl_AndNoLocalhostIsMadeUp(string environment)
    {
        var resolution = Resolve(new(), environment);

        resolution.Url.Should().BeNull();
        resolution.Source.Should().BeNull();
        resolution.LocalEnvironment.Should().BeFalse();
    }

    [Fact]
    public void WithoutAHostEnvironment_TheEnvironmentCountsAsNotLocal()
    {
        var provider = new PublicBaseUrlProvider(new ConfigurationBuilder().Build());

        provider.Resolve().Url.Should().BeNull();
    }

    [Fact]
    public void GetBaseUrl_WithNothingConfigured_SaysWhichKeyToSet()
    {
        var provider = PublicBaseUrlTestSupport.ProviderFor(new Dictionary<string, string?>());

        var act = () => provider.GetBaseUrl();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Frontend:BaseUrl*Frontend__BaseUrl*");
    }

    [Fact]
    public void ConfigurationIsReadAtEveryCall_SoAReloadIsPickedUp()
    {
        var source = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var provider = PublicBaseUrlTestSupport.ProviderFor(source);

        provider.Resolve().Url.Should().BeNull();

        source["Frontend:BaseUrl"] = "https://later.example.test";

        provider.Resolve().Url.Should().Be("https://later.example.test");
    }

    /// <summary>
    /// The shipped <c>appsettings.json</c> defaults neither key. A default there ends the search before
    /// <c>FRONTEND_BASE_URL</c> is looked at, and is what made a missing value in staging come out as a link to
    /// <c>localhost:3000</c> (#4774). Same regression guard as the reranker URL (#3334).
    /// </summary>
    [Fact]
    public void TheShippedAppsettings_DefaultNeitherKey_SoTheEnvironmentVariableIsReached()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FRONTEND_BASE_URL"] = "http://localhost:13000" })
            .Build();

        configuration["App:BaseUrl"].Should().BeNull("a default there would shadow FRONTEND_BASE_URL");
        configuration["Frontend:BaseUrl"].Should().BeNull("a default there would shadow FRONTEND_BASE_URL");

        var resolution = PublicBaseUrlTestSupport.ProviderFor(configuration, "Development").Resolve();

        resolution.Url.Should().Be("http://localhost:13000");
        resolution.Source.Should().Be("FRONTEND_BASE_URL");
    }
}

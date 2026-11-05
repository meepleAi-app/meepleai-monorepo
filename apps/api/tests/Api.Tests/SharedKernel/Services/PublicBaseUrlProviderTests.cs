using Api.SharedKernel.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    [InlineData("CI")]
    [InlineData("Integration")]
    [InlineData("ci")]
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

    // `CI` (the hosts the CI workflows start on the runner) and `Integration` (compose.integration.yml) run on a
    // machine no link ever leaves, and no workflow sets a front-end key for them: they are local, see above.
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
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

    /// <summary>
    /// The provider takes <c>IHostEnvironment?</c> with a default of <c>null</c>, and <c>null</c> counts as not
    /// local. If the container did not hand the real environment over, <c>Development</c> would find no
    /// <c>localhost</c> and <c>make dev</c> would break with nothing pointing at the registration. So the
    /// registration is exercised over a real host, not over the fake of <see cref="PublicBaseUrlTestSupport"/>.
    /// </summary>
    [Theory]
    [InlineData("Development", true, "http://localhost:3000")]
    [InlineData("Production", false, null)]
    public void TheRegisteredProvider_IsGivenTheEnvironmentOfARealHost(
        string environmentName, bool expectedLocal, string? expectedUrl)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = environmentName,
            // No appsettings and no process environment: a FRONTEND_BASE_URL on the machine running the test
            // must not decide the outcome. What is under test is the registration.
            DisableDefaults = true,
        });
        builder.Services.AddPublicBaseUrl();
        using var host = builder.Build();

        var resolution = host.Services.GetRequiredService<IPublicBaseUrlProvider>().Resolve();

        resolution.LocalEnvironment.Should().Be(expectedLocal);
        resolution.Url.Should().Be(expectedUrl);
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

using Api.SharedKernel.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedKernel.Services;

/// <summary>
/// #4774: at startup, outside a local environment, a public front-end URL that is missing or points at this
/// machine is an error in the log: never silent, never a failed start.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class PublicBaseUrlStartupCheckTests
{
    private static (bool Usable, RecordingLogger<PublicBaseUrlStartupCheckTests> Log) Check(
        string environmentName, params (string Key, string Value)[] configured)
    {
        var values = configured.ToDictionary(c => c.Key, c => (string?)c.Value, StringComparer.Ordinal);
        var provider = PublicBaseUrlTestSupport.ProviderFor(values, environmentName);
        var log = new RecordingLogger<PublicBaseUrlStartupCheckTests>();

        var usable = PublicBaseUrlStartupCheck.LogIfMisconfigured(provider, log);

        return (usable, log);
    }

    private static IEnumerable<string> Errors(RecordingLogger<PublicBaseUrlStartupCheckTests> log) =>
        log.Entries.Where(e => e.StartsWith("Error:", StringComparison.Ordinal));

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void NothingConfigured_OutsideDevelopment_IsAnErrorThatNamesTheKeyToSet(string environment)
    {
        var (usable, log) = Check(environment);

        usable.Should().BeFalse();
        var error = Errors(log).Should().ContainSingle().Subject;
        error.Should().Contain("Frontend:BaseUrl").And.Contain("Frontend__BaseUrl");
    }

    [Theory]
    [InlineData("http://localhost:3000")]
    [InlineData("http://localhost:13000")]
    [InlineData("http://127.0.0.1:3000")]
    [InlineData("https://LOCALHOST")]
    public void ALoopbackUrl_OutsideDevelopment_IsAnErrorThatNamesTheValueAndTheKey(string url)
    {
        var (usable, log) = Check("Production", ("Frontend:BaseUrl", url));

        usable.Should().BeFalse();
        var error = Errors(log).Should().ContainSingle().Subject;
        error.Should().Contain(url.TrimEnd('/')).And.Contain("Frontend:BaseUrl");
    }

    [Fact]
    public void TheLegacyKeyHoldingLocalhost_IsFlaggedWithItsSource()
    {
        // The shape of the old shipped default, reaching a deployed host through App:BaseUrl.
        var (usable, log) = Check("Staging", ("App:BaseUrl", "http://localhost:3000"));

        usable.Should().BeFalse();
        Errors(log).Should().ContainSingle().Which.Should().Contain("App:BaseUrl");
    }

    [Fact]
    public void APublicUrl_OutsideDevelopment_IsNotAnError()
    {
        var (usable, log) = Check("Production", ("Frontend:BaseUrl", "https://meepleai.app"));

        usable.Should().BeTrue();
        Errors(log).Should().BeEmpty();
        log.Entries.Should().ContainSingle().Which.Should().Contain("https://meepleai.app");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void ALocalEnvironment_NeverRaisesTheError_WhateverTheValue(string environment)
    {
        Check(environment).Log.Entries.Should().NotContain(e => e.StartsWith("Error:", StringComparison.Ordinal));
        Check(environment, ("Frontend:BaseUrl", "http://localhost:13000")).Log.Entries
            .Should().NotContain(e => e.StartsWith("Error:", StringComparison.Ordinal));
    }

    [Fact]
    public void TheCheckReportsAndReturns_ItNeverThrows()
    {
        var act = () => Check("Production");

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("http://localhost:3000", true)]
    [InlineData("HTTP://LocalHost", true)]
    [InlineData("http://127.0.0.1:8080", true)]
    [InlineData("http://127.0.0.2", true)]
    [InlineData("http://[::1]:3000", true)]
    [InlineData("http://web.localhost:3000", true)]
    [InlineData("localhost:3000", true)]
    [InlineData("https://meepleai.app", false)]
    [InlineData("https://notlocalhost.example", false)]
    [InlineData("https://app.example.test/localhost", false)]
    public void PointsAtThisMachine_RecognisesLoopbackAddresses(string url, bool expected)
    {
        PublicBaseUrlStartupCheck.PointsAtThisMachine(url).Should().Be(expected);
    }
}

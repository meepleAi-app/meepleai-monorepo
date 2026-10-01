using Api.Extensions;
using Api.Tests.Constants;
using Microsoft.Extensions.Configuration;
using Xunit;
using FluentAssertions;

namespace Api.Tests.Extensions;

/// <summary>
/// Regression tests for the defect that kept the snapshot bake red for five weeks (#3998).
/// <para>
/// <c>IConfiguration.GetValue&lt;bool&gt;(key, default)</c> uses the default only when the key is
/// MISSING. A key that is present and holds the empty string gets converted, and
/// <c>BooleanConverter</c> → <c>bool.Parse("")</c> throws. Docker Compose makes that the normal
/// case: <c>DISABLE_RATE_LIMITING: ${DISABLE_RATE_LIMITING:-}</c> expands to the empty string
/// whenever the variable is not in the host environment, so an unset optional switch arrives as
/// present-but-empty. The throw happened inside <c>ConfigureMiddlewarePipeline</c>, before Kestrel
/// started listening: no HTTP surface, nothing in the logs.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "3998")]
public class WebApplicationExtensionsReadFlagTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)))
            .Build();

    /// <summary>
    /// The exact shape the bake produced. This is the case that threw.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void ReadFlag_PresentButEmpty_UsesTheDefault(string empty)
    {
        var config = Config(("DISABLE_RATE_LIMITING", empty));

        WebApplicationExtensions.ReadFlag(config, "DISABLE_RATE_LIMITING", defaultValue: false).Should().BeFalse();
        WebApplicationExtensions.ReadFlag(config, "DISABLE_RATE_LIMITING", defaultValue: true).Should().BeTrue();
    }

    /// <summary>
    /// The baseline that `GetValue` already handled, and that must keep working.
    /// </summary>
    [Fact]
    public void ReadFlag_AbsentKey_UsesTheDefault()
    {
        var config = Config(("Unrelated:Key", "whatever"));

        WebApplicationExtensions.ReadFlag(config, "DISABLE_RATE_LIMITING", defaultValue: false).Should().BeFalse();
        WebApplicationExtensions.ReadFlag(config, "DISABLE_RATE_LIMITING", defaultValue: true).Should().BeTrue();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    public void ReadFlag_ParsableValue_WinsOverTheDefault(string raw, bool expected)
    {
        var config = Config(("DISABLE_RATE_LIMITING", raw));

        // Il default opposto, per provare che e' il valore a decidere e non il fallback.
        WebApplicationExtensions.ReadFlag(config, "DISABLE_RATE_LIMITING", defaultValue: !expected)
            .Should().Be(expected);
    }

    /// <summary>
    /// Deliberate: a non-empty value that cannot be parsed still throws. The comment this fix
    /// replaced claimed that <c>"1"</c> and <c>"yes"</c> behaved like <c>"true"</c> — they never
    /// did, because <c>GetValue&lt;bool&gt;</c> goes through <c>bool.Parse</c>, which accepts only
    /// <c>true</c>/<c>false</c>. Keeping the throw is the point: a typo in a security switch must
    /// stay loud, which is what #3887 set out to achieve. Only the EMPTY case was wrong.
    /// </summary>
    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("yes")]
    [InlineData("on")]
    [InlineData("disabled")]
    public void ReadFlag_NonEmptyButUnparsable_StillThrows(string raw)
    {
        var config = Config(("DISABLE_RATE_LIMITING", raw));

        var act = () => WebApplicationExtensions.ReadFlag(config, "DISABLE_RATE_LIMITING", defaultValue: false);

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Lo stesso difetto valeva per l'altro switch della stessa espressione, che non ha una env
    /// var dedicata nel compose ma condivide la lettura.
    /// </summary>
    [Fact]
    public void ReadFlag_RateLimitingEnabled_EmptyDoesNotDisableRateLimiting()
    {
        var config = Config(("RateLimiting:Enabled", ""), ("DISABLE_RATE_LIMITING", ""));

        // La composizione reale in ConfigureAuthMiddleware: enabled && !disabled.
        var rateLimitingEnabled =
            WebApplicationExtensions.ReadFlag(config, "RateLimiting:Enabled", defaultValue: true)
            && !WebApplicationExtensions.ReadFlag(config, "DISABLE_RATE_LIMITING", defaultValue: false);

        // Due valori vuoti devono lasciare il rate limiting ATTIVO: il fail-safe non deve aprire.
        rateLimitingEnabled.Should().BeTrue();
    }

    /// <summary>
    /// Il difetto nella sua forma originale, per fissare che non e' il test a essere permissivo:
    /// <c>GetValue&lt;bool&gt;</c> su una stringa vuota lancia davvero. Se un domani il
    /// comportamento del framework cambiasse, questo test lo direbbe e ReadFlag diventerebbe
    /// superfluo — non il contrario.
    /// </summary>
    [Fact]
    public void GetValue_OnEmptyString_Throws_WhichIsWhyReadFlagExists()
    {
        var config = Config(("DISABLE_RATE_LIMITING", ""));

        var act = () => config.GetValue("DISABLE_RATE_LIMITING", false);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*DISABLE_RATE_LIMITING*");
    }
}

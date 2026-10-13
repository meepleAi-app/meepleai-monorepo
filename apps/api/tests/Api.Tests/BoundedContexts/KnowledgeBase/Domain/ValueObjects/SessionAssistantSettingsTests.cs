using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Spec 2026-10-11 §3.2: la sezione «Assistente in partita» di una versione del profilo. I valori di
/// default sono quelli misurati dallo spike (§7.1): temperatura 0,2, 600 token, tre giri, 15 secondi.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4275")]
public sealed class SessionAssistantSettingsTests
{
    private static Dictionary<string, string> Prompts() => new(StringComparer.Ordinal)
    {
        [" IT "] = "Sei l'assistente al tavolo.",
        ["en"] = "You are the table assistant.",
    };

    [Fact]
    public void Disabled_IsOff_WithTheMeasuredDefaults()
    {
        var disabled = SessionAssistantSettings.Disabled;

        disabled.Enabled.Should().BeFalse();
        disabled.SystemPrompts.Should().BeEmpty();
        disabled.Temperature.Should().Be(0.2m);
        disabled.MaxResponseTokens.Should().Be(600);
        disabled.MaxToolIterations.Should().Be(3);
        disabled.TurnTimeoutSeconds.Should().Be(15);
        disabled.UseReserveModel.Should().BeFalse();
    }

    [Fact]
    public void Create_NormalizesTheLanguages_AndKeepsTheValues()
    {
        var settings = SessionAssistantSettings.Create(
            enabled: true, Prompts(), temperature: 0.4m, maxResponseTokens: 800, maxToolIterations: 4,
            turnTimeoutSeconds: 20, useReserveModel: true);

        settings.Enabled.Should().BeTrue();
        settings.SystemPrompts.Keys.Should().Equal("en", "it");
        settings.Temperature.Should().Be(0.4m);
        settings.MaxResponseTokens.Should().Be(800);
        settings.MaxToolIterations.Should().Be(4);
        settings.TurnTimeoutSeconds.Should().Be(20);
        settings.UseReserveModel.Should().BeTrue();
    }

    [Theory]
    [InlineData(2.1, 600, 3, 15)]
    [InlineData(-0.1, 600, 3, 15)]
    [InlineData(0.2, 0, 3, 15)]
    [InlineData(0.2, 600, 0, 15)]
    [InlineData(0.2, 600, 9, 15)]
    [InlineData(0.2, 600, 3, 4)]
    [InlineData(0.2, 600, 3, 121)]
    public void Create_OutOfRange_IsRejected(double temperature, int maxResponseTokens, int maxToolIterations, int turnTimeoutSeconds)
    {
        var act = () => SessionAssistantSettings.Create(
            true, Prompts(), (decimal)temperature, maxResponseTokens, maxToolIterations, turnTimeoutSeconds);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Create_Enabled_WithoutPrompts_IsRejected()
    {
        var act = () => SessionAssistantSettings.Create(true, new Dictionary<string, string>(StringComparer.Ordinal));

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Create_Disabled_WithoutPrompts_IsAllowed()
    {
        var settings = SessionAssistantSettings.Create(false, new Dictionary<string, string>(StringComparer.Ordinal));

        settings.Should().Be(SessionAssistantSettings.Disabled);
    }

    [Fact]
    public void Create_TheSameLanguageTwice_IsRejected()
    {
        var prompts = new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = "uno", ["IT "] = "due" };

        var act = () => SessionAssistantSettings.Create(true, prompts);

        act.Should().Throw<ValidationException>();
    }

    [Theory]
    [InlineData("it", "Sei l'assistente al tavolo.")]
    [InlineData("en-US", "You are the table assistant.")]
    [InlineData("EN_gb", "You are the table assistant.")]
    [InlineData("de", null)]
    [InlineData(null, null)]
    public void PromptFor_FallsBackFromTheRegionalVariantToTheLanguage(string? language, string? expected)
    {
        var settings = SessionAssistantSettings.Create(true, Prompts());

        settings.PromptFor(language).Should().Be(expected);
    }

    [Fact]
    public void TwoSettings_DifferingInOneField_AreNotEqual()
    {
        var a = SessionAssistantSettings.Create(true, Prompts());
        var b = SessionAssistantSettings.Create(true, Prompts(), maxToolIterations: 4);

        a.Should().NotBe(b);
        a.Should().Be(SessionAssistantSettings.Create(true, Prompts()));
    }
}

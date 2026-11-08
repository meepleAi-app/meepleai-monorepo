using System.Text.Json;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Spec 2026-10-11 §3.2: il catalogo dice quali modelli supportano il tool calling. Review Focus 1: i
/// tre metodi che ricostruiscono <see cref="ModelSettings"/> non devono azzerare la capacità, o un
/// aggiornamento del prezzo spegnerebbe l'assistente.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "4275")]
public sealed class AiModelConfigurationToolCallingTests
{
    private static AiModelConfiguration FlashWithTools()
    {
        var model = AiModelConfiguration.Create(
            "deepseek-flash", "DeepSeek Flash", "DeepSeek", priority: 8,
            new ModelSettings(4096, 0.7m, new ModelPricing(0.30m, 1.20m)), isActive: false);
        model.DeclareToolCalling(true);
        return model;
    }

    [Fact]
    public void SettingsJson_WrittenBeforeThisChange_HasNoToolCalling()
    {
        const string legacy =
            """{"MaxTokens":4096,"Temperature":0.7,"Pricing":{"InputPricePerMillion":0.30,"OutputPricePerMillion":1.20,"Currency":"USD"}}""";

        JsonSerializer.Deserialize<ModelSettings>(legacy)!.SupportsToolCalling.Should().BeFalse();
    }

    [Fact]
    public void SettingsJson_RoundTripsTheCapability()
    {
        var settings = new ModelSettings(4096, 0.7m, new ModelPricing(0.30m, 1.20m)) { SupportsToolCalling = true };

        var json = JsonSerializer.Serialize(settings);

        json.Should().Contain("\"SupportsToolCalling\":true");
        JsonSerializer.Deserialize<ModelSettings>(json)!.SupportsToolCalling.Should().BeTrue();
    }

    [Fact]
    public void DeclareToolCalling_ChangesTheCapability_AndSaysWhetherItChanged()
    {
        var model = AiModelConfiguration.Create(
            "deepseek-flash", "DeepSeek Flash", "DeepSeek", priority: 8,
            new ModelSettings(4096, 0.7m, new ModelPricing(0.30m, 1.20m)));

        model.DeclareToolCalling(true).Should().BeTrue();
        model.Settings.SupportsToolCalling.Should().BeTrue();
        model.UpdatedAt.Should().NotBeNull();
        model.DeclareToolCalling(true).Should().BeFalse("era già dichiarata");
    }

    [Fact]
    public void UpdatePricing_KeepsTheToolCallingCapability()
    {
        var model = FlashWithTools();

        model.UpdatePricing(0.35m, 1.30m);

        model.Settings.SupportsToolCalling.Should().BeTrue();
        model.Settings.Pricing.InputPricePerMillion.Should().Be(0.35m);
    }

    [Fact]
    public void ApplyVerifiedPricing_KeepsTheToolCallingCapability()
    {
        var model = FlashWithTools();
        var verified = new ModelPricing(0.28m, 1.10m, verifiedOn: new DateOnly(2026, 10, 12), source: "https://example.test/pricing");

        model.ApplyVerifiedPricing(verified).Should().BeTrue();

        model.Settings.SupportsToolCalling.Should().BeTrue();
        model.Settings.Pricing.InputPricePerMillion.Should().Be(0.28m);
    }

    [Fact]
    public void UpdateSettings_KeepsTheToolCallingCapability()
    {
        var model = FlashWithTools();

        model.UpdateSettings(2048, 0.5m);

        model.Settings.SupportsToolCalling.Should().BeTrue();
        model.Settings.MaxTokens.Should().Be(2048);
    }
}

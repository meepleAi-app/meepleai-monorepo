using System.Text.Json;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// #4164 (ADR-095): prezzi verificati alla fonte nel catalogo modelli.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public sealed class AiModelConfigurationVerifiedPricingTests
{
    private static readonly DateOnly October10 = new(2026, 10, 10);

    private static AiModelConfiguration Model(ModelPricing pricing) =>
        AiModelConfiguration.Create(
            "deepseek-chat", "DeepSeek Chat", "DeepSeek", priority: 1,
            new ModelSettings(4096, 0.7m, pricing));

    private static ModelPricing Verified(decimal input, decimal output, DateOnly on) =>
        new(input, output, cacheReadPricePerMillion: 0.006m, verifiedOn: on, source: "https://example.test/pricing");

    [Fact]
    public void ApplyVerifiedPricing_OverAnUnverifiedPrice_Applies()
    {
        var model = Model(new ModelPricing(0.14m, 0.28m));

        model.ApplyVerifiedPricing(Verified(0.30m, 1.20m, October10)).Should().BeTrue();

        model.Settings.Pricing.InputPricePerMillion.Should().Be(0.30m);
        model.Settings.Pricing.OutputPricePerMillion.Should().Be(1.20m);
        model.Settings.Pricing.CacheReadPricePerMillion.Should().Be(0.006m);
        model.Settings.Pricing.VerifiedOn.Should().Be(October10);
        model.Settings.Pricing.Source.Should().Be("https://example.test/pricing");
        model.Settings.MaxTokens.Should().Be(4096, "si cambia solo il prezzo");
    }

    [Fact]
    public void ApplyVerifiedPricing_OverANewerVerification_IsIgnored()
    {
        var model = Model(Verified(0.50m, 2.00m, October10.AddDays(1)));

        model.ApplyVerifiedPricing(Verified(0.30m, 1.20m, October10)).Should().BeFalse();

        model.Settings.Pricing.InputPricePerMillion.Should().Be(0.50m);
    }

    [Fact]
    public void ApplyVerifiedPricing_SameDay_IsIgnored()
    {
        var model = Model(Verified(0.50m, 2.00m, October10));

        model.ApplyVerifiedPricing(Verified(0.30m, 1.20m, October10)).Should().BeFalse();
    }

    [Fact]
    public void ApplyVerifiedPricing_WithoutAVerificationDate_Throws()
    {
        var model = Model(ModelPricing.Free);

        var act = () => model.ApplyVerifiedPricing(new ModelPricing(1m, 1m));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdatePricing_ByTheAdmin_IsAVerificationMadeToday_AndKeepsTheCachePrice()
    {
        var model = Model(Verified(0.30m, 1.20m, October10));

        model.UpdatePricing(0.40m, 1.60m);

        var pricing = model.Settings.Pricing;
        pricing.InputPricePerMillion.Should().Be(0.40m);
        pricing.OutputPricePerMillion.Should().Be(1.60m);
        pricing.CacheReadPricePerMillion.Should().Be(0.006m);
        pricing.VerifiedOn.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
        pricing.Source.Should().Be(AiModelConfiguration.AdminPricingSource);
    }

    [Fact]
    public void AnAdminPriceEnteredToday_BeatsASeedVerifiedEarlier()
    {
        var model = Model(ModelPricing.Free);
        model.UpdatePricing(0.40m, 1.60m);

        model.ApplyVerifiedPricing(Verified(0.30m, 1.20m, October10)).Should().BeFalse();
    }

    [Fact]
    public void SettingsJson_WrittenBefore4164_StillDeserializes_WithoutVerification()
    {
        // La forma di settings_json scritta dal repository prima di #4164.
        const string legacy =
            """{"MaxTokens":4096,"Temperature":0.7,"Pricing":{"InputPricePerMillion":0.19,"OutputPricePerMillion":0.87,"Currency":"USD"}}""";

        var settings = JsonSerializer.Deserialize<ModelSettings>(legacy)!;

        settings.Pricing.InputPricePerMillion.Should().Be(0.19m);
        settings.Pricing.Currency.Should().Be("USD");
        settings.Pricing.CacheReadPricePerMillion.Should().BeNull();
        settings.Pricing.VerifiedOn.Should().BeNull();
        settings.Pricing.Source.Should().BeNull();
    }

    [Fact]
    public void SettingsJson_RoundTripsTheVerification()
    {
        var settings = new ModelSettings(4096, 0.7m, Verified(0.30m, 1.20m, October10));

        var roundTripped = JsonSerializer.Deserialize<ModelSettings>(JsonSerializer.Serialize(settings))!;

        roundTripped.Pricing.Should().Be(settings.Pricing);
    }

    [Fact]
    public void ModelPricing_NegativeCachePrice_Throws()
    {
        var act = () => new ModelPricing(1m, 1m, cacheReadPricePerMillion: -0.1m);

        act.Should().Throw<ArgumentException>();
    }
}

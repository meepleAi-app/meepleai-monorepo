using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.SystemConfiguration.Application.Commands.Tier;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Unit.Tier;

/// <summary>
/// Issue #4212: a client that leaves <c>maxLibraryGames</c> out of the limits sends 0. The domain
/// rejects it (a library that holds nothing would block every add of the tier), but as a bare
/// <see cref="ArgumentException"/>: a 400 that names no field. The validators say which field.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public sealed class TierDefinitionCommandValidatorsTests
{
    private static TierLimitsDto LimitsWithLibrary(int maxLibraryGames) =>
        TierLimitsDto.FromValueObject(TierLimits.FreeTier) with { MaxLibraryGames = maxLibraryGames };

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Create_RejectsALibraryLimitBelowOne_NamingTheField(int maxLibraryGames)
    {
        var command = new CreateTierDefinitionCommand(
            "silver", "Silver", LimitsWithLibrary(maxLibraryGames), "free");

        var result = new CreateTierDefinitionCommandValidator(Options.Create(new PdfProcessingOptions())).Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Limits.MaxLibraryGames");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Update_RejectsALibraryLimitBelowOne_NamingTheField(int maxLibraryGames)
    {
        var command = new UpdateTierDefinitionCommand(
            "free", null, LimitsWithLibrary(maxLibraryGames), null, null);

        var result = new UpdateTierDefinitionCommandValidator(Options.Create(new PdfProcessingOptions())).Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Limits.MaxLibraryGames");
    }

    [Fact]
    public void Create_AcceptsALibraryLimitOfOne()
    {
        var command = new CreateTierDefinitionCommand("silver", "Silver", LimitsWithLibrary(1), "free");

        new CreateTierDefinitionCommandValidator(Options.Create(new PdfProcessingOptions())).Validate(command).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_WithoutLimits_IsStillValid()
    {
        var command = new UpdateTierDefinitionCommand("free", null, null, "standard", null);

        new UpdateTierDefinitionCommandValidator(Options.Create(new PdfProcessingOptions())).Validate(command).IsValid.Should().BeTrue();
    }

    // #4449: a plan's PDF size is a commercial cap under the single PDF limit, never above it.
    private static readonly IOptions<PdfProcessingOptions> PdfLimit = Options.Create(new PdfProcessingOptions());

    private static TierLimitsDto LimitsWithPdfSize(long maxPdfSizeBytes) =>
        TierLimitsDto.FromValueObject(TierLimits.FreeTier) with { MaxPdfSizeBytes = maxPdfSizeBytes };

    [Fact]
    public void Create_RejectsAPdfSizeAboveThePdfLimit_NamingTheField()
    {
        var command = new CreateTierDefinitionCommand(
            "gold", "Gold", LimitsWithPdfSize(PdfLimit.Value.MaxFileSizeBytes + 1), "free");

        var result = new CreateTierDefinitionCommandValidator(PdfLimit).Validate(command);

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Limits.MaxPdfSizeBytes")
            .Which.ErrorMessage.Should().Contain("100 MB");
    }

    [Fact]
    public void Update_RejectsAPdfSizeAboveThePdfLimit_NamingTheField()
    {
        var command = new UpdateTierDefinitionCommand(
            "premium", null, LimitsWithPdfSize(200L * 1024 * 1024), null, null);

        var result = new UpdateTierDefinitionCommandValidator(PdfLimit).Validate(command);

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Limits.MaxPdfSizeBytes");
    }

    [Fact]
    public void Update_AcceptsAPdfSizeAtThePdfLimit()
    {
        var command = new UpdateTierDefinitionCommand(
            "premium", null, LimitsWithPdfSize(PdfLimit.Value.MaxFileSizeBytes), null, null);

        new UpdateTierDefinitionCommandValidator(PdfLimit).Validate(command).IsValid.Should().BeTrue();
    }

    [Fact]
    public void TheSeededPlans_AreValidUnderThePdfLimit()
    {
        // The premium default was 200 MB: a seeded plan the editor would then refuse to save.
        foreach (var limits in new[] { TierLimits.FreeTier, TierLimits.NormalTier, TierLimits.PremiumTier })
        {
            limits.MaxPdfSizeBytes.Should().BeLessThanOrEqualTo(PdfLimit.Value.MaxFileSizeBytes);
        }
    }
}

using Api.BoundedContexts.SystemConfiguration.Application.Commands.Tier;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
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

        var result = new CreateTierDefinitionCommandValidator().Validate(command);

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

        var result = new UpdateTierDefinitionCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Limits.MaxLibraryGames");
    }

    [Fact]
    public void Create_AcceptsALibraryLimitOfOne()
    {
        var command = new CreateTierDefinitionCommand("silver", "Silver", LimitsWithLibrary(1), "free");

        new CreateTierDefinitionCommandValidator().Validate(command).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_WithoutLimits_IsStillValid()
    {
        var command = new UpdateTierDefinitionCommand("free", null, null, "standard", null);

        new UpdateTierDefinitionCommandValidator().Validate(command).IsValid.Should().BeTrue();
    }
}

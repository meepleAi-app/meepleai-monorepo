using Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateStatusBanner;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Validators;

[Trait("Category", TestCategories.Unit)]
public sealed class UpdateStatusBannerValidatorTests
{
    private readonly UpdateStatusBannerValidator _validator = new();

    [Fact]
    public void Valid_ActiveCommand_Passes()
    {
        var cmd = new UpdateStatusBannerCommand("hello", "Warning", true, null, null, "admin");
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Valid_InactiveWithEmptyMessage_Passes()
    {
        var cmd = new UpdateStatusBannerCommand("", "Info", false, null, null, null);
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Invalid_ActiveWithEmptyMessage_Fails()
    {
        var cmd = new UpdateStatusBannerCommand("", "Info", true, null, null, null);
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(cmd.Message));
    }

    [Fact]
    public void Invalid_MessageTooLong_Fails()
    {
        var cmd = new UpdateStatusBannerCommand(
            new string('x', IncidentBannerState.MaxMessageLength + 1),
            "Info", true, null, null, null);

        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Info")]
    [InlineData("warning")]
    [InlineData("CRITICAL")]
    public void Valid_SeverityCaseInsensitive_Passes(string severity)
    {
        var cmd = new UpdateStatusBannerCommand("hi", severity, true, null, null, null);
        _validator.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Invalid_UnknownSeverity_Fails()
    {
        var cmd = new UpdateStatusBannerCommand("hi", "Bogus", true, null, null, null);
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(cmd.Severity));
    }

    [Fact]
    public void Invalid_StartsAtAfterEndsAt_Fails()
    {
        var t = new DateTime(2026, 5, 17, 12, 0, 0, DateTimeKind.Utc);
        var cmd = new UpdateStatusBannerCommand("hi", "Info", true, StartsAt: t.AddHours(2), EndsAt: t.AddHours(1), UpdatedBy: null);
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }
}

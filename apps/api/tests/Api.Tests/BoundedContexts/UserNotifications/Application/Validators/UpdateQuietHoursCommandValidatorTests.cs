using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Validators;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Validators;

/// <summary>
/// Issue #2995: validation guards that turn a bad quiet-hours payload into a 400 (FluentValidation)
/// instead of a 500 from a bubbling domain <see cref="ArgumentException"/> (project rule #2568).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserNotifications")]
public sealed class UpdateQuietHoursCommandValidatorTests
{
    private readonly UpdateQuietHoursCommandValidator _validator = new();

    private static UpdateQuietHoursCommand Cmd(
        string timeZone = "Europe/Rome", string? start = "22:00", string? end = "08:00", Guid? userId = null) =>
        new(userId ?? Guid.NewGuid(), timeZone, start, end);

    [Fact]
    public void Valid_WindowWithIanaTimeZone_Passes()
    {
        _validator.Validate(Cmd()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Valid_ClearedWindow_NullStartAndEnd_Passes()
    {
        _validator.Validate(Cmd(timeZone: "UTC", start: null, end: null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Invalid_EmptyUserId_Fails()
    {
        var result = _validator.Validate(Cmd(userId: Guid.Empty));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateQuietHoursCommand.UserId));
    }

    [Fact]
    public void Invalid_EmptyTimeZone_Fails()
    {
        var result = _validator.Validate(Cmd(timeZone: ""));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateQuietHoursCommand.TimeZone));
    }

    [Fact]
    public void Invalid_UnknownTimeZone_Fails()
    {
        var result = _validator.Validate(Cmd(timeZone: "Not/AZone"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateQuietHoursCommand.TimeZone));
    }

    [Fact]
    public void Invalid_OnlyStartSet_Fails()
    {
        var result = _validator.Validate(Cmd(start: "22:00", end: null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "QuietHours");
    }

    [Fact]
    public void Invalid_OnlyEndSet_Fails()
    {
        var result = _validator.Validate(Cmd(start: null, end: "08:00"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "QuietHours");
    }

    [Fact]
    public void Invalid_UnparseableTime_Fails()
    {
        var result = _validator.Validate(Cmd(start: "25:99", end: "08:00"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateQuietHoursCommand.QuietHoursStart));
    }
}

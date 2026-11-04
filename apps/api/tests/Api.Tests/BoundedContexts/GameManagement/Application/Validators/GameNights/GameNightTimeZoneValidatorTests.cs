using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.Validators.GameNights;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Validators.GameNights;

/// <summary>
/// #4754. The zone a client sends with a game night is checked for SHAPE only, by the one rule both validators
/// share, so that POST and PUT answer the same input the same way. Whether the server knows the zone is not
/// asked: the zone only feeds a display, and the email formatter says UTC when it cannot use it.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4754")]
public sealed class GameNightTimeZoneValidatorTests
{
    private readonly CreateGameNightCommandValidator _create = new();
    private readonly UpdateGameNightCommandValidator _update = new();

    private static CreateGameNightCommand NewCreate(string? timeZoneId) => new(
        UserId: Guid.NewGuid(),
        Title: "Test Night",
        ScheduledAt: DateTimeOffset.UtcNow.AddHours(24),
        TimeZoneId: timeZoneId);

    private static UpdateGameNightCommand NewUpdate(string? timeZoneId) => new(
        GameNightId: Guid.NewGuid(),
        UserId: Guid.NewGuid(),
        Title: "Test Night",
        ScheduledAt: DateTimeOffset.UtcNow.AddHours(24),
        TimeZoneId: timeZoneId);

    public static TheoryData<string?> Acceptable => new()
    {
        null,
        "",
        "   ",
        "UTC",
        "Europe/Rome",
        "America/Argentina/Buenos_Aires",
        "America/Port-au-Prince",
        "Etc/GMT+5",
        "Asia/Kolkata",
    };

    public static TheoryData<string> Rejected => new()
    {
        "Europe/Rome; DROP TABLE game_night_events",
        "<script>alert(1)</script>",
        "Europe Rome",
        "/Europe/Rome",
        "Europe//Rome",
        "Europe/Rome/",
        "Europe/../Rome",
        "1Europe/Rome",
        "Europe/Ro\nme",
        "Eu/ro/pe/Ro/me", // more levels than any IANA id has
    };

    [Theory]
    [MemberData(nameof(Acceptable))]
    public void Create_AcceptsAnAbsentBlankOrWellFormedZone(string? zone)
    {
        _create.TestValidate(NewCreate(zone)).ShouldNotHaveValidationErrorFor(x => x.TimeZoneId);
    }

    [Theory]
    [MemberData(nameof(Acceptable))]
    public void Update_AcceptsAnAbsentBlankOrWellFormedZone(string? zone)
    {
        _update.TestValidate(NewUpdate(zone)).ShouldNotHaveValidationErrorFor(x => x.TimeZoneId);
    }

    [Theory]
    [MemberData(nameof(Rejected))]
    public void Create_RejectsAMalformedZone(string zone)
    {
        _create.TestValidate(NewCreate(zone)).ShouldHaveValidationErrorFor(x => x.TimeZoneId);
    }

    [Theory]
    [MemberData(nameof(Rejected))]
    public void Update_RejectsAMalformedZone(string zone)
    {
        _update.TestValidate(NewUpdate(zone)).ShouldHaveValidationErrorFor(x => x.TimeZoneId);
    }

    [Fact]
    public void BothVerbs_RejectAZoneLongerThanTheColumn()
    {
        var tooLong = "Europe/" + new string('a', 60);

        _create.TestValidate(NewCreate(tooLong)).ShouldHaveValidationErrorFor(x => x.TimeZoneId);
        _update.TestValidate(NewUpdate(tooLong)).ShouldHaveValidationErrorFor(x => x.TimeZoneId);
    }

    [Fact]
    public void TheLengthLimitIsTheWidthOfTheColumn()
    {
        GameNightTimeZoneRules.IsAcceptable(new string('a', 64)).Should().BeTrue();
        GameNightTimeZoneRules.IsAcceptable(new string('a', 65)).Should().BeFalse();
    }
}

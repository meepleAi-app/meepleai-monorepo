using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// #4754. <c>ScheduledAt</c> is an instant and carries no zone, so an email could only print UTC: a night
/// the organizer set for 21:00 in Rome went out as 8:00 PM. The aggregate now keeps the IANA zone the
/// organizer scheduled in.
/// </summary>
/// <remarks>
/// The aggregate does not decide whether an id names a real zone (that would make a night fail to be created
/// over a value that only feeds a display); it keeps what it is given, trimmed, and turns a blank into
/// "not sent". On update, "not sent" keeps the stored zone — an older client that does not know the field
/// must not erase it by moving the time.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4754")]
public sealed class GameNightEventTimeZoneTests
{
    private static readonly DateTimeOffset Later = DateTimeOffset.UtcNow.AddDays(7);

    private static GameNightEvent NewNight(string? timeZoneId) =>
        GameNightEvent.Create(Guid.NewGuid(), "Serata di prova", Later, timeZoneId: timeZoneId);

    [Fact]
    public void Create_KeepsTheZoneTheOrganizerScheduledIn()
    {
        NewNight("Europe/Rome").TimeZoneId.Should().Be("Europe/Rome");
    }

    [Fact]
    public void Create_WithoutAZone_HasNone()
    {
        NewNight(null).TimeZoneId.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithABlankZone_IsTheSameAsNotSent(string blank)
    {
        NewNight(blank).TimeZoneId.Should().BeNull();
    }

    [Fact]
    public void Create_TrimsTheZone()
    {
        NewNight("  Europe/Rome ").TimeZoneId.Should().Be("Europe/Rome");
    }

    [Fact]
    public void Create_WithAZoneLongerThanTheColumn_Throws()
    {
        var tooLong = new string('a', GameNightEvent.TimeZoneIdMaxLength + 1);

        var act = () => NewNight(tooLong);

        act.Should().Throw<ArgumentException>().WithParameterName("timeZoneId");
    }

    [Fact]
    public void Create_DoesNotJudgeWhetherTheZoneIsReal()
    {
        // A zone this host does not know must not stop a night from being created; the email says UTC.
        NewNight("Mars/Olympus").TimeZoneId.Should().Be("Mars/Olympus");
    }

    [Fact]
    public void Update_WithAZone_ReplacesIt()
    {
        var night = NewNight("Europe/Rome");

        night.Update("Serata di prova", null, Later.AddDays(1), null, null, null, "America/New_York");

        night.TimeZoneId.Should().Be("America/New_York");
    }

    [Fact]
    public void Update_WithoutAZone_KeepsTheStoredOne()
    {
        var night = NewNight("Europe/Rome");

        // The time moves; the client (an older build) does not send a zone.
        night.Update("Serata di prova", null, Later.AddDays(1), null, null, null);

        night.TimeZoneId.Should().Be("Europe/Rome");
    }

    [Fact]
    public void Update_WithABlankZone_KeepsTheStoredOne()
    {
        var night = NewNight("Europe/Rome");

        night.Update("Serata di prova", null, Later.AddDays(1), null, null, null, "  ");

        night.TimeZoneId.Should().Be("Europe/Rome");
    }

    [Fact]
    public void Update_GivesAZoneToANightThatHadNone()
    {
        var night = NewNight(null);

        night.Update("Serata di prova", null, Later.AddDays(1), null, null, null, "Europe/Rome");

        night.TimeZoneId.Should().Be("Europe/Rome");
    }

    [Fact]
    public void Update_WithAZoneLongerThanTheColumn_ThrowsAndChangesNothing()
    {
        var night = NewNight("Europe/Rome");
        var tooLong = new string('a', GameNightEvent.TimeZoneIdMaxLength + 1);

        var act = () => night.Update("Altro titolo", null, Later.AddDays(1), null, null, null, tooLong);

        act.Should().Throw<ArgumentException>();
        night.TimeZoneId.Should().Be("Europe/Rome");
        night.Title.Should().Be("Serata di prova", "a rejected update leaves the night as it was");
    }
}

using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain.Aggregates;

[Trait("Category", "Unit")]
public class NotificationPreferencesQuietHoursTests
{
    private static NotificationPreferences CreatePrefs() => new(Guid.NewGuid());

    [Fact]
    public void DefaultPrefs_QuietHoursNotActive()
    {
        var prefs = CreatePrefs();

        prefs.TimeZone.Should().Be("UTC");
        prefs.QuietHoursStart.Should().BeNull();
        prefs.QuietHoursEnd.Should().BeNull();
        prefs.IsQuietHoursActive(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void UpdateQuietHours_ValidWindow_PersistsFields()
    {
        var prefs = CreatePrefs();

        prefs.UpdateQuietHours("Europe/Rome", new TimeOnly(22, 0), new TimeOnly(8, 0));

        prefs.TimeZone.Should().Be("Europe/Rome");
        prefs.QuietHoursStart.Should().Be(new TimeOnly(22, 0));
        prefs.QuietHoursEnd.Should().Be(new TimeOnly(8, 0));
    }

    [Fact]
    public void UpdateQuietHours_NullStartAndEnd_ClearsWindow()
    {
        var prefs = CreatePrefs();
        prefs.UpdateQuietHours("Europe/Rome", new TimeOnly(22, 0), new TimeOnly(8, 0));

        prefs.UpdateQuietHours("Europe/Rome", null, null);

        prefs.QuietHoursStart.Should().BeNull();
        prefs.QuietHoursEnd.Should().BeNull();
    }

    [Fact]
    public void UpdateQuietHours_OnlyStartSet_Throws()
    {
        var prefs = CreatePrefs();

        var act = () => prefs.UpdateQuietHours("UTC", new TimeOnly(22, 0), null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*both be set or both be null*");
    }

    [Fact]
    public void UpdateQuietHours_EmptyTimeZone_Throws()
    {
        var prefs = CreatePrefs();

        var act = () => prefs.UpdateQuietHours("", new TimeOnly(22, 0), new TimeOnly(8, 0));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Time zone cannot be empty*");
    }

    [Fact]
    public void IsQuietHoursActive_NormalWindow_ActiveDuring()
    {
        var prefs = CreatePrefs();
        prefs.UpdateQuietHours("UTC", new TimeOnly(9, 0), new TimeOnly(17, 0));

        // 12:00 UTC is inside [09:00, 17:00)
        var insideWindow = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        prefs.IsQuietHoursActive(insideWindow).Should().BeTrue();

        // 08:00 UTC is BEFORE window start
        var beforeWindow = new DateTimeOffset(2026, 6, 15, 8, 0, 0, TimeSpan.Zero);
        prefs.IsQuietHoursActive(beforeWindow).Should().BeFalse();

        // 17:00 UTC is at window end (exclusive)
        var atWindowEnd = new DateTimeOffset(2026, 6, 15, 17, 0, 0, TimeSpan.Zero);
        prefs.IsQuietHoursActive(atWindowEnd).Should().BeFalse();
    }

    [Fact]
    public void IsQuietHoursActive_CrossMidnightWindow_ActiveOvernight()
    {
        var prefs = CreatePrefs();
        prefs.UpdateQuietHours("UTC", new TimeOnly(22, 0), new TimeOnly(8, 0));

        // 23:00 UTC (after start, before midnight)
        var lateNight = new DateTimeOffset(2026, 6, 15, 23, 0, 0, TimeSpan.Zero);
        prefs.IsQuietHoursActive(lateNight).Should().BeTrue();

        // 03:00 UTC (after midnight, before end)
        var earlyMorning = new DateTimeOffset(2026, 6, 16, 3, 0, 0, TimeSpan.Zero);
        prefs.IsQuietHoursActive(earlyMorning).Should().BeTrue();

        // 12:00 UTC (well outside window)
        var midday = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        prefs.IsQuietHoursActive(midday).Should().BeFalse();
    }

    [Fact]
    public void IsQuietHoursActive_TimeZoneAwareness_ConvertsUtcCorrectly()
    {
        // Quiet hours 22:00-08:00 LOCAL Europe/Rome (UTC+2 in summer, UTC+1 in winter)
        var prefs = CreatePrefs();
        prefs.UpdateQuietHours("Europe/Rome", new TimeOnly(22, 0), new TimeOnly(8, 0));

        // 20:30 UTC on 2026-06-15 (summer time CEST UTC+2) = 22:30 local Rome → inside window
        var utcInsideRomeWindow = new DateTimeOffset(2026, 6, 15, 20, 30, 0, TimeSpan.Zero);
        prefs.IsQuietHoursActive(utcInsideRomeWindow).Should().BeTrue();

        // 12:00 UTC on 2026-06-15 = 14:00 local Rome → outside window
        var utcOutsideRomeWindow = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        prefs.IsQuietHoursActive(utcOutsideRomeWindow).Should().BeFalse();
    }

    [Fact]
    public void UpdateQuietHours_WhitespaceTimeZone_Throws()
    {
        var prefs = CreatePrefs();

        var act = () => prefs.UpdateQuietHours("   ", new TimeOnly(22, 0), new TimeOnly(8, 0));

        act.Should().Throw<ArgumentException>();
    }
}

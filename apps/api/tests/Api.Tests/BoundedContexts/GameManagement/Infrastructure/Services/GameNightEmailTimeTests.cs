using Api.BoundedContexts.GameManagement.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// #4754: how the game night emails turn a scheduled instant into the words a guest reads — the Italian names,
/// what counts as "the zone could not be used", and the old zone names a browser still reports.
/// </summary>
/// <remarks>
/// <para>
/// The old names: a V8 browser reports some zones under a name that Ubuntu 24.04 with only <c>tzdata</c>
/// (no <c>tzdata-legacy</c>) has no file for — <c>Asia/Calcutta</c>, <c>Europe/Kiev</c>, <c>America/Buenos_Aires</c>…
/// The server then could not resolve the zone and wrote the time in UTC, which is the defect this work removes,
/// back again for an organizer in Delhi. The formatter maps the old name to the canonical one BEFORE it asks the
/// host. The id the organizer's browser sent is what stays stored and what the email prints; only the lookup
/// uses the canonical name.
/// </para>
/// <para>
/// These tests give the formatter a host that has no file for the old names (<see cref="NobleWithoutLegacy"/>),
/// so they fail without the mapping on every machine; asking the real host instead would pass on a Windows
/// machine whose ICU knows the old names by itself, and on an image that ships <c>tzdata-legacy</c>.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4754")]
public sealed class GameNightEmailTimeTests
{
    /// <summary>
    /// The old name → canonical name pairs a V8 browser reports and Ubuntu 24.04 with only <c>tzdata</c> does
    /// not ship as a file. Written out here on purpose: an independent copy, so a changed target in the
    /// formatter is a failing test and not a silently changed expectation.
    /// </summary>
    public static TheoryData<string, string> LegacyZoneNames => new()
    {
        { "Africa/Asmera", "Africa/Asmara" },
        { "America/Buenos_Aires", "America/Argentina/Buenos_Aires" },
        { "America/Catamarca", "America/Argentina/Catamarca" },
        { "America/Cordoba", "America/Argentina/Cordoba" },
        { "America/Godthab", "America/Nuuk" },
        { "America/Indianapolis", "America/Indiana/Indianapolis" },
        { "America/Jujuy", "America/Argentina/Jujuy" },
        { "America/Louisville", "America/Kentucky/Louisville" },
        { "America/Mendoza", "America/Argentina/Mendoza" },
        { "Asia/Calcutta", "Asia/Kolkata" },
        { "Asia/Katmandu", "Asia/Kathmandu" },
        { "Asia/Rangoon", "Asia/Yangon" },
        { "Asia/Saigon", "Asia/Ho_Chi_Minh" },
        { "Atlantic/Faeroe", "Atlantic/Faroe" },
        { "Europe/Kiev", "Europe/Kyiv" },
        { "Pacific/Enderbury", "Pacific/Kanton" },
        { "Pacific/Ponape", "Pacific/Pohnpei" },
        { "Pacific/Truk", "Pacific/Chuuk" },
    };

    private static readonly DateTimeOffset Noon = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private static TimeZoneInfo? OnThisHost(string id) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : null;

    /// <summary>A host that knows every canonical name and none of the old ones (Ubuntu 24.04, only tzdata).</summary>
    private static TimeZoneInfo? NobleWithoutLegacy(string id) =>
        LegacyZoneNames.Any(pair => string.Equals(pair.Data.Item1, id, StringComparison.Ordinal))
            ? null
            : OnThisHost(id);

    private static string Format(string? zone, bool italian, Func<string, TimeZoneInfo?> host, DateTimeOffset? at = null) =>
        GameNightEmailTime.FormatWhen(at ?? Noon, zone, italian, host, out _);

    // ── The old names ────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(LegacyZoneNames))]
    public void AnOldZoneName_OnAHostWithoutIt_WritesTheSameTimeAsTheCanonicalName(string oldName, string canonical)
    {
        var viaOld = GameNightEmailTime.FormatWhen(Noon, oldName, italian: true, NobleWithoutLegacy, out var resolved);
        var viaCanonical = Format(canonical, italian: true, NobleWithoutLegacy);

        resolved.Should().BeTrue($"{oldName} must resolve through {canonical}");
        viaOld.Should().NotContain("(UTC)", "that is the fallback this mapping exists to avoid");
        // Same wall clock and offset; the label carries the name the browser sent, not the canonical one.
        viaOld.Should().Be(viaCanonical.Replace($"({canonical},", $"({oldName},", StringComparison.Ordinal));
    }

    [Fact]
    public void AnOldName_GivesTheRightTimeWithTheItalianLabel()
    {
        // 12:00Z is 17:30 in India; the organizer's browser said Asia/Calcutta.
        var text = Format("Asia/Calcutta", italian: true, NobleWithoutLegacy);

        text.Should().Be("venerdì 30 ottobre 2026 alle 17:30 (Asia/Calcutta, UTC+5:30)");
    }

    [Fact]
    public void WithoutTheMapping_TheSameHostCannotResolveAnOldName_SoTheFixtureIsHonest()
    {
        // The fixture host itself: an old name is unknown to it, a canonical one is not. If this failed, the
        // tests above would pass whatever the formatter did with the old names.
        NobleWithoutLegacy("Asia/Calcutta").Should().BeNull();
        NobleWithoutLegacy("Asia/Kolkata").Should().NotBeNull();
    }

    [Fact]
    public void TheMappingIsExactlyTheExpectedPairs()
    {
        var expected = LegacyZoneNames.ToDictionary(p => p.Data.Item1, p => p.Data.Item2, StringComparer.Ordinal);

        GameNightEmailTime.LegacyZoneIds.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [MemberData(nameof(LegacyZoneNames))]
    public void EveryCanonicalTarget_IsAZoneThisHostResolves_AndNoNameMapsToItself(string oldName, string canonical)
    {
        OnThisHost(canonical).Should().NotBeNull($"{canonical} is where {oldName} is sent");
        canonical.Should().NotBe(oldName);
    }

    [Fact]
    public void ACanonicalName_IsNotChanged()
    {
        Format("Europe/Rome", italian: true, OnThisHost).Should().Contain("(Europe/Rome, UTC+1)");
    }

    // ── What counts as "the zone could not be used" ─────────────────────────

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("Europe/Rome", true)]
    [InlineData("Asia/Calcutta", true)]
    [InlineData("Mars/Olympus", false)]
    [InlineData("not a zone", false)]
    public void ZoneResolved_IsFalseOnlyWhenAZoneWasGivenAndCouldNotBeUsed(string? zone, bool expected)
    {
        GameNightEmailTime.FormatWhen(Noon, zone, italian: false, NobleWithoutLegacy, out var resolved);

        resolved.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Mars/Olympus")]
    public void NoZoneOrAnUnusableOne_WritesUtcAndSaysSo(string? zone)
    {
        Format(zone, italian: false, NobleWithoutLegacy).Should().Be("Friday, October 30, 2026 at 12:00 PM (UTC)");
    }

    [Fact]
    public void AHostThatKnowsNoZone_FallsBackToUtc()
    {
        var text = GameNightEmailTime.FormatWhen(Noon, "Europe/Rome", italian: true, _ => null, out var resolved);

        resolved.Should().BeFalse();
        text.Should().Be("venerdì 30 ottobre 2026 alle 12:00 (UTC)");
    }

    // ── The Italian names ────────────────────────────────────────────────────

    /// <summary>
    /// Every day of one week (2026-10-25 is a Sunday), so a misspelt name cannot hide behind the one day the
    /// other tests happen to use.
    /// </summary>
    public static TheoryData<int, string> ItalianDays => new()
    {
        { 25, "domenica" },
        { 26, "lunedì" },
        { 27, "martedì" },
        { 28, "mercoledì" },
        { 29, "giovedì" },
        { 30, "venerdì" },
        { 31, "sabato" },
    };

    public static TheoryData<int, string> ItalianMonths => new()
    {
        { 1, "gennaio" },
        { 2, "febbraio" },
        { 3, "marzo" },
        { 4, "aprile" },
        { 5, "maggio" },
        { 6, "giugno" },
        { 7, "luglio" },
        { 8, "agosto" },
        { 9, "settembre" },
        { 10, "ottobre" },
        { 11, "novembre" },
        { 12, "dicembre" },
    };

    [Theory]
    [MemberData(nameof(ItalianDays))]
    public void ItalianDayName_IsSpelledOutForEveryDayOfTheWeek(int dayOfOctober, string name)
    {
        var at = new DateTimeOffset(2026, 10, dayOfOctober, 12, 0, 0, TimeSpan.Zero);

        Format("UTC", italian: true, OnThisHost, at).Should().Be($"{name} {dayOfOctober} ottobre 2026 alle 12:00 (UTC)");
    }

    [Theory]
    [MemberData(nameof(ItalianMonths))]
    public void ItalianMonthName_IsSpelledOutForEveryMonth(int month, string name)
    {
        var at = new DateTimeOffset(2026, month, 15, 12, 0, 0, TimeSpan.Zero);

        Format("UTC", italian: true, OnThisHost, at).Should().Contain($" 15 {name} 2026 alle 12:00 (UTC)");
    }
}

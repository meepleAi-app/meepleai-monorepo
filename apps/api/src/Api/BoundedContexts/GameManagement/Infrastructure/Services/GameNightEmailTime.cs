using System.Globalization;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// The single place where the game night emails (invitation, 24 h reminder, cancellation, RSVP confirmation)
/// turn a scheduled instant into the words a guest reads (#4754).
/// </summary>
/// <remarks>
/// <para>
/// <c>GameNightEvent.ScheduledAt</c> is an instant (UTC). Each template used to print it with the same
/// <c>ToString("dddd, MMMM d, yyyy 'at' h:mm tt")</c> and no zone, so a night the organizer set for 21:00 in
/// Rome went out as "8:00 PM": one hour early, with nothing saying it was UTC. Here the instant is read on
/// the night's own zone and the zone is named next to the time.
/// </para>
/// <para>
/// The label is the zone id plus its UTC offset at that instant ("Europe/Rome, UTC+1"), not an abbreviation:
/// .NET has no CET/CEST, and an invented abbreviation is worse than none. The id printed is the one the
/// client sent, not <see cref="TimeZoneInfo.Id"/>, which on Windows is the Windows name of the same zone.
/// </para>
/// <para>
/// Nothing here throws on a zone it cannot use. A missing zone, a zone this host does not know, or a host
/// without tzdata gives the time in UTC, labelled "UTC" — the one thing that must not happen is the unlabelled
/// UTC reading this replaces. The caller learns of the fallback through <c>zoneResolved</c> and logs it.
/// Italian names are spelled out below rather than taken from <see cref="CultureInfo"/>, so the text does not
/// depend on ICU data being present in the container.
/// </para>
/// <para>
/// Old zone names: a V8 browser reports some zones under a name that Ubuntu 24.04 with only <c>tzdata</c> (no
/// <c>tzdata-legacy</c>) has no file for — <c>Asia/Calcutta</c>, <c>Europe/Kiev</c>, <c>America/Buenos_Aires</c>…
/// <see cref="LegacyZoneIds"/> maps them to the canonical name BEFORE the host is asked, so an organizer in Delhi
/// gets 21:00 and not a UTC fallback. The id the browser sent is what is stored and what the email prints; only the
/// lookup uses the canonical name. An image that ships <c>tzdata-legacy</c> would make the map redundant; the map
/// is what keeps this correct on a host that does not.
/// </para>
/// </remarks>
internal static class GameNightEmailTime
{
    private static readonly string[] ItalianDays =
        ["domenica", "lunedì", "martedì", "mercoledì", "giovedì", "venerdì", "sabato"];

    private static readonly string[] ItalianMonths =
    [
        "gennaio", "febbraio", "marzo", "aprile", "maggio", "giugno",
        "luglio", "agosto", "settembre", "ottobre", "novembre", "dicembre"
    ];

    /// <summary>
    /// The names a browser reports for a zone and the canonical name the same zone has in the current tz database
    /// (V8 reports the old name for some zones; the canonical one is a file on every host, the old one is not).
    /// Only names that a plain Ubuntu 24.04 <c>tzdata</c> lacks as files are listed, which is what the
    /// <c>Intl</c> of a current browser can report; a name this table does not know is looked up as it is.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> LegacyZoneIds { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Africa/Asmera"] = "Africa/Asmara",
            ["America/Buenos_Aires"] = "America/Argentina/Buenos_Aires",
            ["America/Catamarca"] = "America/Argentina/Catamarca",
            ["America/Cordoba"] = "America/Argentina/Cordoba",
            ["America/Godthab"] = "America/Nuuk",
            ["America/Indianapolis"] = "America/Indiana/Indianapolis",
            ["America/Jujuy"] = "America/Argentina/Jujuy",
            ["America/Louisville"] = "America/Kentucky/Louisville",
            ["America/Mendoza"] = "America/Argentina/Mendoza",
            ["Asia/Calcutta"] = "Asia/Kolkata",
            ["Asia/Katmandu"] = "Asia/Kathmandu",
            ["Asia/Rangoon"] = "Asia/Yangon",
            ["Asia/Saigon"] = "Asia/Ho_Chi_Minh",
            ["Atlantic/Faeroe"] = "Atlantic/Faroe",
            ["Europe/Kiev"] = "Europe/Kyiv",
            ["Pacific/Enderbury"] = "Pacific/Kanton",
            ["Pacific/Ponape"] = "Pacific/Pohnpei",
            ["Pacific/Truk"] = "Pacific/Chuuk",
        };

    /// <summary>
    /// "venerdì 30 ottobre 2026 alle 21:00 (Europe/Rome, UTC+1)" or, in English,
    /// "Friday, October 30, 2026 at 9:00 PM (Europe/Rome, UTC+1)". The result is HTML-safe: it can be placed in the
    /// email body as it is.
    /// </summary>
    /// <param name="scheduledAt">The instant the night starts.</param>
    /// <param name="timeZoneId">The night's IANA zone, or null/blank when none was stored.</param>
    /// <param name="italian">Italian wording of the date; English otherwise.</param>
    /// <param name="zoneResolved">False when a zone was given but could not be used and UTC was written instead.</param>
    public static string FormatWhen(
        DateTimeOffset scheduledAt,
        string? timeZoneId,
        bool italian,
        out bool zoneResolved) =>
        FormatWhen(scheduledAt, timeZoneId, italian, FindOnThisHost, out zoneResolved);

    /// <summary>
    /// <see cref="FormatWhen(DateTimeOffset, string?, bool, out bool)"/> with the host's zone lookup passed in, so
    /// a test can stand in for a host that lacks some names (the real lookup answers differently on Windows, on
    /// Ubuntu with <c>tzdata-legacy</c> and on Ubuntu without it).
    /// </summary>
    internal static string FormatWhen(
        DateTimeOffset scheduledAt,
        string? timeZoneId,
        bool italian,
        Func<string, TimeZoneInfo?> findZone,
        out bool zoneResolved)
    {
        var requested = timeZoneId?.Trim();
        var resolved = TryResolve(requested, findZone, out var zone);
        zoneResolved = resolved || string.IsNullOrEmpty(requested);

        var local = resolved
            ? TimeZoneInfo.ConvertTime(scheduledAt, zone!)
            : scheduledAt.ToUniversalTime();
        var label = resolved ? ZoneLabel(requested!, local.Offset) : "UTC";

        var when = italian
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{ItalianDays[(int)local.DayOfWeek]} {local.Day} {ItalianMonths[local.Month - 1]} {local.Year} alle {local:HH:mm}")
            : local.ToString("dddd, MMMM d, yyyy 'at' h:mm tt", CultureInfo.InvariantCulture);

        return $"{when} ({label})";
    }

    private static bool TryResolve(string? timeZoneId, Func<string, TimeZoneInfo?> findZone, out TimeZoneInfo? zone)
    {
        zone = null;
        if (string.IsNullOrEmpty(timeZoneId))
            return false;

        // The name the browser sent may be an old one the host has no file for: ask for the canonical name.
        var lookupId = LegacyZoneIds.TryGetValue(timeZoneId, out var canonical) ? canonical : timeZoneId;
        zone = findZone(lookupId);
        return zone is not null;
    }

    private static TimeZoneInfo? FindOnThisHost(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone) ? zone : null;
        }
        catch (InvalidTimeZoneException)
        {
            // The host has an entry for it but the data is unreadable: the same as not knowing the zone.
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string ZoneLabel(string requestedId, TimeSpan offset)
    {
        var offsetText = FormatOffset(offset);

        // The id came from a client. A zone the host resolved has no HTML-significant character in its name, but
        // the result goes straight into the email body, so it is encoded here rather than trusted.
        return string.Equals(requestedId, "UTC", StringComparison.OrdinalIgnoreCase)
            || string.Equals(requestedId, "Etc/UTC", StringComparison.OrdinalIgnoreCase)
            ? "UTC"
            : $"{System.Net.WebUtility.HtmlEncode(requestedId)}, {offsetText}";
    }

    private static string FormatOffset(TimeSpan offset)
    {
        if (offset == TimeSpan.Zero)
            return "UTC";

        var sign = offset < TimeSpan.Zero ? '-' : '+';
        var abs = offset.Duration();
        return abs.Minutes == 0
            ? string.Create(CultureInfo.InvariantCulture, $"UTC{sign}{abs.Hours}")
            : string.Create(CultureInfo.InvariantCulture, $"UTC{sign}{abs.Hours}:{abs.Minutes:00}");
    }
}

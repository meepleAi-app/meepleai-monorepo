using System.Globalization;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// The single place the four game night emails turn a scheduled instant into the words a guest reads (#4754).
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
        out bool zoneResolved)
    {
        var requested = timeZoneId?.Trim();
        var resolved = TryResolve(requested, out var zone);
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

    private static bool TryResolve(string? timeZoneId, out TimeZoneInfo? zone)
    {
        zone = null;
        if (string.IsNullOrEmpty(timeZoneId))
            return false;

        try
        {
            return TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out zone);
        }
        catch (InvalidTimeZoneException)
        {
            // The host has an entry for it but the data is unreadable: the same as not knowing the zone.
            zone = null;
            return false;
        }
        catch (ArgumentException)
        {
            zone = null;
            return false;
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

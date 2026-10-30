namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// What a game night email needs to know to speak the organizer's language and to say the time the way the
/// organizer meant it (#4754).
/// </summary>
/// <param name="Language">
/// The organizer's <c>User.Language</c> ("it", "en", "it-IT"…), or null when it is not known.
/// Italian gets Italian; anything else, or nothing, gets English (the language every template had before).
/// </param>
/// <param name="TimeZoneId">
/// The IANA zone stored on the night, or null. Null — or an id this server cannot resolve — makes the
/// email give the time in UTC and say so: never an unlabelled UTC reading that looks local.
/// </param>
internal sealed record GameNightEmailLocale(string? Language, string? TimeZoneId)
{
    /// <summary>English, UTC: what an email says when nothing about the organizer or the night is known.</summary>
    public static GameNightEmailLocale Unknown { get; } = new(Language: null, TimeZoneId: null);
}

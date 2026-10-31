namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// The name a player gets at the table when there is no usable one (#4752). A table name is shown to every
/// player of the game — on the live page, in the diary, as the winner, in the play record — so the fallback
/// must never identify the user: NOT the e-mail address, nor any part of it. The strings are Italian like
/// the rest of the roster the server writes.
///
/// <para>«No usable name» is a blank name <b>or one that is an e-mail address</b>. The second case is not
/// hypothetical: the user store fills a missing display name with the address (<c>UserRepository</c> maps
/// it that way), so a user without a name reaches the handlers as <c>DisplayName == Email</c>; and a
/// tracking Session joined elsewhere can carry the address as a participant name. A blank check alone would
/// never fire on real data.</para>
/// </summary>
internal static class RosterDisplayNames
{
    /// <summary>A seated player with no usable name; also what a blank requested name becomes.</summary>
    public const string Player = "Giocatore";

    /// <summary>The organizer of the night, seated by the server, with no usable name.</summary>
    public const string Organizer = "Organizzatore";

    public static string Resolve(string? displayName, string fallback) =>
        string.IsNullOrWhiteSpace(displayName) || LooksLikeAnEmailAddress(displayName) ? fallback : displayName;

    /// <summary>One «@» with something before it, a dot in the domain, no spaces: the shape of an address.</summary>
    internal static bool LooksLikeAnEmailAddress(string value)
    {
        var name = value.Trim();
        var at = name.IndexOf('@', StringComparison.Ordinal);

        return at > 0
            && at == name.LastIndexOf('@')
            && name.IndexOf('.', at) > at + 1
            && !name.Any(char.IsWhiteSpace);
    }
}

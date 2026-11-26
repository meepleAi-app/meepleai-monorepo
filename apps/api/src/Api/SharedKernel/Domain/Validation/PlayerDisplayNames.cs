namespace Api.SharedKernel.Domain.Validation;

/// <summary>
/// The name other players see for a user: at the table, in a night's roster, in a session's participants.
/// It is never the user's e-mail address, written as it is or inside a longer name (#4752, #4780).
/// </summary>
/// <remarks>
/// <para>
/// The address reaches the name in two ways. <c>UserRepository</c> fills a missing name with it, so a user
/// without one arrives as <c>DisplayName == Email</c>; and the readers that went to the database wrote
/// <c>DisplayName ?? Email</c> themselves.
/// </para>
/// <para>
/// When the account's address is known, a name that contains it goes, even when it is not only the address:
/// <c>Marta (marta@example.com)</c>, or <c>user@localhost</c>, which has no shape of an address. A name with the shape
/// of an address goes in any case, even when it is not the account's (decision of #4780): the filler written before
/// an address changed is the old address, which the new one does not find. <c>DJ@Home.TV</c> goes with it, and it can
/// only come from OAuth or an admin, since registration and the profile refuse an «@» in a name.
/// </para>
/// <para>The strings are Italian like the rest of the roster the server writes.</para>
/// </remarks>
internal static class PlayerDisplayNames
{
    /// <summary>A player with no usable name.</summary>
    public const string Player = "Giocatore";

    /// <summary>The organizer of a night, with no usable name.</summary>
    public const string Organizer = "Organizzatore";

    /// <summary>The name to show other players for a user with <paramref name="email"/>; <paramref name="fallback"/> when there is none.</summary>
    public static string ForOthers(string? displayName, string? email, string fallback)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return fallback;

        var name = displayName.Trim();
        var address = email?.Trim();

        if (!string.IsNullOrEmpty(address) && name.Contains(address, StringComparison.OrdinalIgnoreCase))
            return fallback;

        return LooksLikeAnEmailAddress(name) ? fallback : name;
    }

    /// <summary><see cref="ForOthers"/>, cut to the limit of a seat (<see cref="PlayerNameLimit"/>).</summary>
    public static string ForTheTable(string? displayName, string? email, string fallback) =>
        PlayerNameLimit.Clamp(ForOthers(displayName, email, fallback));

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

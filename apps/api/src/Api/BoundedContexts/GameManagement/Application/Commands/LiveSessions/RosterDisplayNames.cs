using Api.SharedKernel.Domain.Validation;

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
///
/// <para>#4780: the rule lives in <see cref="PlayerDisplayNames"/>, which the other bounded contexts use too.
/// With the account's address at hand (<see cref="Resolve(string?, string?, string)"/>) the name is compared with
/// it; without (<see cref="Resolve(string?, string)"/>, a name from the tracking roster or typed by hand) the
/// shape of an address decides.</para>
/// </summary>
internal static class RosterDisplayNames
{
    /// <summary>A seated player with no usable name; also what a blank requested name becomes.</summary>
    public const string Player = PlayerDisplayNames.Player;

    /// <summary>The organizer of the night, seated by the server, with no usable name.</summary>
    public const string Organizer = PlayerDisplayNames.Organizer;

    /// <remarks>
    /// #5150: the name leaves cut to <see cref="PlayerNameLimit.MaxLength"/>. Every name the server seats on its
    /// own comes through here (the night's organizer and players, the go-live's organizer), and a profile name
    /// reaches 128 characters: seeded whole, the tracking session refused it and the whole start answered 422.
    /// </remarks>
    public static string Resolve(string? displayName, string fallback) =>
        PlayerDisplayNames.ForTheTable(displayName, email: null, fallback);

    /// <summary>The name of a user whose address is known: compared with it, not judged by its shape.</summary>
    public static string Resolve(string? displayName, string? email, string fallback) =>
        PlayerDisplayNames.ForTheTable(displayName, email, fallback);

    /// <summary>One «@» with something before it, a dot in the domain, no spaces: the shape of an address.</summary>
    internal static bool LooksLikeAnEmailAddress(string value) => PlayerDisplayNames.LooksLikeAnEmailAddress(value);
}

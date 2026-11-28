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

    /// <summary>
    /// #4780 PR 4 — the name a player takes at the table: <see cref="Resolve(string?, string)"/>, cut to
    /// <see cref="PlayerNameLimit.MaxLength"/>, and made unique among <paramref name="usedNames"/> ignoring case with a
    /// « (2)», « (3)»… suffix. The live game seats its players this way, and the night start names its tracking
    /// participants this way too: a guest is matched to their live seat by name when the game closes
    /// (<c>CompleteNightLinkForLiveSessionCommand.LiveRanks</c>), so the two must agree, also for two guests both
    /// left with the neutral name. It agrees with <c>LiveGameSession.AddPlayer</c>, which compares names after
    /// <c>Trim().ToLowerInvariant()</c> and caps them at <see cref="PlayerNameLimit"/> (#5150).
    /// </summary>
    /// <param name="requested">The name asked for.</param>
    /// <param name="usedNames">The names already taken, lowercased; the chosen one is added.</param>
    public static string Unique(string? requested, ISet<string> usedNames)
    {
        ArgumentNullException.ThrowIfNull(usedNames);

        var baseName = Resolve(requested, Player).Trim();
        var candidate = PlayerNameLimit.Clamp(baseName, PlayerNameLimit.MaxLength);
        var suffix = 2;

        while (!usedNames.Add(candidate.ToLowerInvariant()))
        {
            var tail = $" ({suffix++})";
            candidate = PlayerNameLimit.Clamp(baseName, PlayerNameLimit.MaxLength - tail.Length) + tail;
        }

        return candidate;
    }

    /// <summary>One «@» with something before it, a dot in the domain, no spaces: the shape of an address.</summary>
    internal static bool LooksLikeAnEmailAddress(string value) => PlayerDisplayNames.LooksLikeAnEmailAddress(value);
}

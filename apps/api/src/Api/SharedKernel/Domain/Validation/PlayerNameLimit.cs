namespace Api.SharedKernel.Domain.Validation;

/// <summary>
/// The longest name of a player, on every step the name goes through: the seat at the live table, the player
/// of the correlated <c>GameSession</c> that the start fills, its winner, and the tracking participant whose
/// name a go-live seats at the table.
/// </summary>
/// <remarks>
/// #5150: the seat took 100 characters and the correlated game session 50, so a player named with 51 to 100
/// characters was seated without errors and then the game did not start. The user decided on 50 everywhere
/// (2026-11-25). The columns are not all this narrow (<c>session_players.display_name</c> is 100): the limit is
/// the domain's, as for the correlated game session, whose players are stored in a <c>text</c> column.
/// The length is <see cref="string.Length"/>, in UTF-16 code units: an emoji counts two.
/// </remarks>
internal static class PlayerNameLimit
{
    public const int MaxLength = 50;

    /// <summary>
    /// Cuts a name the server seats on its own — a profile name reaches 128 characters — to
    /// <paramref name="maxLength"/>, without trailing spaces and without leaving half of a character outside
    /// the basic plane: a lone surrogate is not valid UTF-8, and Npgsql refuses to write it.
    /// </summary>
    public static string Clamp(string name, int maxLength = MaxLength)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length <= maxLength)
            return name;

        var cut = name[..maxLength];
        if (char.IsHighSurrogate(cut[^1]))
            cut = cut[..^1];

        return cut.TrimEnd();
    }
}

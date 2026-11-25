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
/// </remarks>
internal static class PlayerNameLimit
{
    public const int MaxLength = 50;
}

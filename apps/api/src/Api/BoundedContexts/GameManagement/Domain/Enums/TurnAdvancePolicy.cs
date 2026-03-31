namespace Api.BoundedContexts.GameManagement.Domain.Enums;

/// <summary>
/// Controls how turns advance in a live game session.
/// </summary>
internal enum TurnAdvancePolicy
{
    /// <summary>
    /// The host manually advances to the next turn.
    /// </summary>
    Manual = 0,

    /// <summary>
    /// Each player confirms they are done; the turn advances when all players confirm.
    /// </summary>
    AllPlayersConfirm = 1,

    /// <summary>
    /// The active player confirms they are done; the turn advances automatically.
    /// </summary>
    ActivePlayerConfirms = 2
}

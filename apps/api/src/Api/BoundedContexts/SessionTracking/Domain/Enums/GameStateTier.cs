namespace Api.BoundedContexts.SessionTracking.Domain.Enums;

/// <summary>
/// Tier of game state tracking complexity during a session.
/// Minimal: only current turn/phase (default, backward compatible)
/// Score: scores + turn
/// Full: player resources + character sheet + custom fields
/// </summary>
public enum GameStateTier
{
    Minimal = 0,
    Score = 1,
    Full = 2,
}

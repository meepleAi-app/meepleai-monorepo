namespace Api.BoundedContexts.GameToolkit.Domain.Enums;

/// <summary>
/// How scoring works in a game session.
///
/// **v2 (B19-3b, 2026-05-31)**: extended with BinaryWin / Objectives to cover
/// co-op games (Paleo, Zombicide: win/lose collective) and goal-oriented games
/// (Codenames: race + assassin-loss; Pandemic: cure-all-diseases).
/// Additive — preserves numeric IDs 0-1.
/// </summary>
public enum ScoreType
{
    Points = 0,
    Ranking = 1,
    // v2 additions
    BinaryWin = 2,   // game is win/lose without points (co-op Paleo / Pandemic / Zombicide)
    Objectives = 3   // win condition is meeting a set of objectives (Codenames race, mission games)
}

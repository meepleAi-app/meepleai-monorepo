namespace Api.BoundedContexts.GameToolkit.Domain.Enums;

/// <summary>
/// How turn order is determined in a game session.
///
/// **v2 (B19-3a, 2026-05-31)**: extended with Sequential / Simultaneous / Realtime / None
/// to cover non-circular turn patterns (co-op simultaneous like Paleo, team-deduction like
/// Codenames, real-time like Captain Sonar). Additive — preserves numeric IDs 0-2.
/// </summary>
public enum TurnOrderType
{
    RoundRobin = 0,
    Custom = 1,
    Free = 2,
    // v2 additions
    Sequential = 3,    // strict order but NOT circular (e.g., team-based: Red phase → Blue phase)
    Simultaneous = 4,  // all players act at the same time (e.g., Paleo, 7 Wonders)
    Realtime = 5,      // no turn structure, time-pressure (e.g., Captain Sonar, Magic Maze)
    None = 6           // truly no turn order (e.g., co-op brainstorm without phase structure)
}

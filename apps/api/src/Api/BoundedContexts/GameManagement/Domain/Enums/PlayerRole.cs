namespace Api.BoundedContexts.GameManagement.Domain.Enums;

/// <summary>
/// Role of a player within a live game session.
/// </summary>
public enum PlayerRole
{
    /// <summary>
    /// Session host with full control (pause, advance turn, kick players).
    /// </summary>
    Host = 0,

    /// <summary>
    /// Active participant who can update own state.
    /// </summary>
    Player = 1,

    /// <summary>
    /// View-only observer with chat access.
    /// </summary>
    Spectator = 2
}

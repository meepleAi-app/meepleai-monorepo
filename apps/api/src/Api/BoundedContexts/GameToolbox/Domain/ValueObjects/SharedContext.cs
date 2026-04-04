namespace Api.BoundedContexts.GameToolbox.Domain.ValueObjects;

/// <summary>
/// Player information within a Toolbox session.
/// </summary>
public record PlayerInfo(string Name, string Color, string? AvatarUrl = null);

/// <summary>
/// Shared state visible to all tools in a Toolbox.
/// </summary>
public record SharedContext
{
    public List<PlayerInfo> Players { get; init; } = [];
    public int CurrentPlayerIndex { get; init; }
    public int CurrentRound { get; init; } = 1;
    public Dictionary<string, string> CustomProperties { get; init; } = [];

    /// <summary>
    /// Returns the player whose turn it currently is, or null if no players.
    /// </summary>
    public PlayerInfo? CurrentPlayer =>
        CurrentPlayerIndex >= 0 && CurrentPlayerIndex < Players.Count
            ? Players[CurrentPlayerIndex]
            : null;

    /// <summary>
    /// Advances to the next player, wrapping around to the first.
    /// </summary>
    public SharedContext AdvancePlayer()
    {
        var nextIndex = Players.Count > 0
            ? (CurrentPlayerIndex + 1) % Players.Count
            : 0;
        return this with { CurrentPlayerIndex = nextIndex };
    }

    /// <summary>
    /// Advances to the next round and resets the player index.
    /// </summary>
    public SharedContext AdvanceRound() =>
        this with { CurrentRound = CurrentRound + 1, CurrentPlayerIndex = 0 };
}

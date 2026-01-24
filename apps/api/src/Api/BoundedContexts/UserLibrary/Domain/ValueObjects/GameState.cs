using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.UserLibrary.Domain.ValueObjects;

/// <summary>
/// Represents the state of a game in the user's library.
/// </summary>
public enum GameStateType
{
    /// <summary>
    /// Game is newly acquired, never played.
    /// </summary>
    Nuovo = 0,

    /// <summary>
    /// Game is currently on loan to someone.
    /// </summary>
    InPrestito = 1,

    /// <summary>
    /// Game is on user's wishlist (not owned yet).
    /// </summary>
    Wishlist = 2,

    /// <summary>
    /// Game is owned and available.
    /// </summary>
    Owned = 3
}

/// <summary>
/// Value object representing the state of a game in user's library.
/// Enforces business rules for valid state transitions.
/// </summary>
internal sealed class GameState : ValueObject
{
    /// <summary>
    /// The current state of the game.
    /// </summary>
    public GameStateType Value { get; }

    /// <summary>
    /// Optional timestamp when the state was last changed.
    /// </summary>
    public DateTime? ChangedAt { get; }

    /// <summary>
    /// Optional notes about the state (e.g., who borrowed it, why it's on wishlist).
    /// </summary>
    public string? StateNotes { get; }

    /// <summary>
    /// Creates a new GameState value object.
    /// </summary>
    /// <param name="value">The game state type</param>
    /// <param name="changedAt">Optional timestamp of state change</param>
    /// <param name="stateNotes">Optional notes about the state</param>
    private GameState(GameStateType value, DateTime? changedAt = null, string? stateNotes = null)
    {
        Value = value;
        ChangedAt = changedAt ?? DateTime.UtcNow;
        StateNotes = stateNotes?.Trim();
    }

    /// <summary>
    /// Creates a Nuovo (new, unplayed) state.
    /// </summary>
    public static GameState Nuovo(string? notes = null)
        => new(GameStateType.Nuovo, DateTime.UtcNow, notes);

    /// <summary>
    /// Creates an InPrestito (on loan) state.
    /// </summary>
    /// <param name="borrowerInfo">Information about who borrowed the game</param>
    public static GameState InPrestito(string? borrowerInfo = null)
        => new(GameStateType.InPrestito, DateTime.UtcNow, borrowerInfo);

    /// <summary>
    /// Creates a Wishlist state.
    /// </summary>
    /// <param name="notes">Optional notes about why it's on wishlist</param>
    public static GameState Wishlist(string? notes = null)
        => new(GameStateType.Wishlist, DateTime.UtcNow, notes);

    /// <summary>
    /// Creates an Owned (available) state.
    /// </summary>
    public static GameState Owned(string? notes = null)
        => new(GameStateType.Owned, DateTime.UtcNow, notes);

    /// <summary>
    /// Validates if transition from current state to new state is allowed.
    /// </summary>
    /// <param name="newState">The target state</param>
    /// <returns>True if transition is valid, false otherwise</returns>
    public bool CanTransitionTo(GameStateType newState)
    {
        // Cannot loan a game that's on wishlist (not owned yet)
        if (Value == GameStateType.Wishlist && newState == GameStateType.InPrestito)
            return false;

        // Cannot mark as "Nuovo" if it's already owned or on loan
        if ((Value == GameStateType.Owned || Value == GameStateType.InPrestito)
            && newState == GameStateType.Nuovo)
            return false;

        // All other transitions are valid
        return true;
    }

    /// <summary>
    /// Returns whether the game is currently available for play.
    /// </summary>
    public bool IsAvailable() => Value is GameStateType.Nuovo or GameStateType.Owned;

    /// <summary>
    /// Returns whether the game is physically owned (not wishlist).
    /// </summary>
    public bool IsPhysicallyOwned() => Value != GameStateType.Wishlist;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
        yield return ChangedAt;
        yield return StateNotes;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator GameStateType(GameState state) => state.Value;
}

using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Entities.TurnOrder;

/// <summary>
/// Tracks turn order state for a live game session.
/// Part of the base toolkit — always present regardless of custom toolkit.
/// Advancing past the last player increments RoundNumber and resets to index 0.
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
#pragma warning disable MA0049 // Type name matches containing namespace - intentional DDD folder structure
internal sealed class TurnOrder : Entity<Guid>
#pragma warning restore MA0049
{
    public Guid SessionId { get; private set; }

    /// <summary>Ordered list of player names/identifiers.</summary>
    public IReadOnlyList<string> PlayerOrder { get; private set; }

    /// <summary>Zero-based index of the current player in <see cref="PlayerOrder"/>.</summary>
    public int CurrentIndex { get; private set; }

    /// <summary>1-based round counter. Increments when the order wraps past the last player.</summary>
    public int RoundNumber { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Private constructor for EF Core and Restore()
#pragma warning disable CS8618
    private TurnOrder() : base() { }
#pragma warning restore CS8618

    /// <summary>Creates a new TurnOrder for a session.</summary>
    public TurnOrder(Guid id, Guid sessionId, IReadOnlyList<string> playerOrder) : base(id)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("SessionId cannot be empty.", nameof(sessionId));
        if (playerOrder == null || playerOrder.Count == 0)
            throw new ArgumentException("PlayerOrder must contain at least one player.", nameof(playerOrder));

        SessionId = sessionId;
        PlayerOrder = playerOrder;
        CurrentIndex = 0;
        RoundNumber = 1;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Restores a TurnOrder from persisted state (used by the repository layer).
    /// </summary>
    internal static TurnOrder Restore(
        Guid id,
        Guid sessionId,
        IReadOnlyList<string> playerOrder,
        int currentIndex,
        int roundNumber,
        DateTime createdAt,
        DateTime updatedAt)
    {
        var turnOrder = new TurnOrder
        {
            Id = id,
            SessionId = sessionId,
            PlayerOrder = playerOrder,
            CurrentIndex = currentIndex,
            RoundNumber = roundNumber,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
        return turnOrder;
    }

    /// <summary>Returns the name of the current player.</summary>
    public string CurrentPlayer => PlayerOrder[CurrentIndex];

    /// <summary>Returns the name of the next player (wraps around).</summary>
    public string NextPlayer => PlayerOrder[(CurrentIndex + 1) % PlayerOrder.Count];

    /// <summary>
    /// Advances to the next player. When the last player is passed,
    /// RoundNumber is incremented and CurrentIndex wraps to 0.
    /// </summary>
    /// <returns>The name of the previous player before advancing.</returns>
    public string Advance()
    {
        var previousPlayer = CurrentPlayer;
        CurrentIndex++;

        if (CurrentIndex >= PlayerOrder.Count)
        {
            CurrentIndex = 0;
            RoundNumber++;
        }

        UpdatedAt = DateTime.UtcNow;
        return previousPlayer;
    }

    /// <summary>
    /// Replaces the player order with a new ordered list.
    /// CurrentIndex is clamped to remain valid.
    /// </summary>
    public void Reorder(IReadOnlyList<string> newOrder)
    {
        if (newOrder == null || newOrder.Count == 0)
            throw new ArgumentException("PlayerOrder must contain at least one player.", nameof(newOrder));

        PlayerOrder = newOrder;
        CurrentIndex = Math.Min(CurrentIndex, newOrder.Count - 1);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Resets to round 1, first player.</summary>
    public void Reset()
    {
        CurrentIndex = 0;
        RoundNumber = 1;
        UpdatedAt = DateTime.UtcNow;
    }
}

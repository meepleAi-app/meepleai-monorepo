namespace Api.Models.Requests;

/// <summary>
/// Request model for creating game state snapshot.
/// Issue #2403: GameSessionState Entity (snapshots for undo/history)
/// </summary>
public sealed record CreateStateSnapshotRequest
{
    /// <summary>
    /// Turn number for this snapshot.
    /// </summary>
    public required int TurnNumber { get; init; }

    /// <summary>
    /// Description of the snapshot (e.g., "After player 1 move").
    /// </summary>
    public required string Description { get; init; }
}

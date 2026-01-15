using System.Text.Json;

namespace Api.BoundedContexts.GameManagement.Application;

/// <summary>
/// DTO for game state snapshot.
/// Issue #2403: GameSessionState Entity (snapshots for undo/history)
/// </summary>
public sealed record GameStateSnapshotDto
{
    public Guid Id { get; init; }
    public Guid SessionStateId { get; init; }
    public JsonDocument State { get; init; } = JsonDocument.Parse("{}");
    public int TurnNumber { get; init; }
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}

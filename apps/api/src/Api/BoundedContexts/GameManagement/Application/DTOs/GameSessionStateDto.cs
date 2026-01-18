using System.Text.Json;

namespace Api.BoundedContexts.GameManagement.Application;

/// <summary>
/// DTO for game session state.
/// Issue #2403: GameSessionState Entity
/// </summary>
public sealed record GameSessionStateDto
{
    public Guid Id { get; init; }
    public Guid GameSessionId { get; init; }
    public Guid TemplateId { get; init; }
    public JsonDocument CurrentState { get; init; } = JsonDocument.Parse("{}");
    public int Version { get; init; }
    public DateTime LastUpdatedAt { get; init; }
    public string LastUpdatedBy { get; init; } = string.Empty;
    public List<GameStateSnapshotDto> Snapshots { get; init; } = new();
}

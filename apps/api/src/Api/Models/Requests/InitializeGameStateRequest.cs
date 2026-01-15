using System.Text.Json;

namespace Api.Models.Requests;

/// <summary>
/// Request model for initializing game session state.
/// Issue #2403: GameSessionState Entity
/// </summary>
public sealed record InitializeGameStateRequest
{
    /// <summary>
    /// ID of the GameStateTemplate to use.
    /// </summary>
    public required Guid TemplateId { get; init; }

    /// <summary>
    /// Optional initial state JSON. If null, defaults from template are used.
    /// </summary>
    public JsonDocument? InitialState { get; init; }
}

using System.Text.Json;

namespace Api.Models.Requests;

/// <summary>
/// Request model for updating game session state.
/// Issue #2403: GameSessionState Entity
/// </summary>
public sealed record UpdateGameStateRequest
{
    /// <summary>
    /// New state JSON (can be partial update with JSON Patch).
    /// </summary>
    public required JsonDocument NewState { get; init; }
}

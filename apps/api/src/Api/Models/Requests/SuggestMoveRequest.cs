namespace Api.Models.Requests;

/// <summary>
/// Request to get move suggestions for a game session using Player Mode agent.
/// Issue #2404 - Player Mode move suggestions
/// </summary>
/// <param name="AgentId">Agent configured in Player Mode to use for suggestions</param>
/// <param name="Query">Optional specific question about the move (e.g., "Cosa dovrei fare?")</param>
internal sealed record SuggestMoveRequest(
    Guid AgentId,
    string? Query = null
);

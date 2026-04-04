namespace Api.BoundedContexts.GameManagement.Domain.Entities.PauseSnapshot;

/// <summary>
/// Value record capturing a single player's score at the moment a session is paused.
/// Stored as JSONB inside PauseSnapshot.
/// </summary>
public sealed record PlayerScoreSnapshot(string PlayerName, decimal Score, int? PlayerId = null);

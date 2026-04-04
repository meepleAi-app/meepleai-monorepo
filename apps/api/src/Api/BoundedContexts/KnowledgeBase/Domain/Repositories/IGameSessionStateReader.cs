using System.Text.Json;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Read-only abstraction over game session state, allowing the KnowledgeBase domain
/// to access game state without depending on the GameManagement bounded context.
/// Implementations in the infrastructure layer bridge to the GM repository.
/// </summary>
public interface IGameSessionStateReader
{
    /// <summary>
    /// Gets a read-only snapshot of game session state for the given session.
    /// Returns null if no state exists for the session.
    /// </summary>
    Task<GameSessionStateSnapshot?> GetBySessionIdAsync(
        Guid gameSessionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only snapshot of game session state consumed by KnowledgeBase domain services.
/// </summary>
public sealed record GameSessionStateSnapshot(
    Guid Id,
    Guid GameSessionId,
    JsonDocument? CurrentState,
    DateTime LastUpdatedAt)
{
    /// <summary>Returns the current state as a JSON string, or null if no state.</summary>
    public string? GetStateAsString()
        => CurrentState?.RootElement.GetRawText();
}

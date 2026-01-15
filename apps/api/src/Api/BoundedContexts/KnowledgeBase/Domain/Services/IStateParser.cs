using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using System.Text.Json;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Service contract for parsing natural language messages into game state changes.
/// Issue #2405 - Ledger Mode NLP parsing
/// </summary>
internal interface IStateParser
{
    /// <summary>
    /// Parses a chat message to extract game state changes.
    /// Supports Italian language expressions.
    /// </summary>
    /// <param name="message">The chat message to parse</param>
    /// <param name="currentState">Current game state for context and conflict detection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extraction result with detected state changes and conflicts</returns>
    Task<StateExtractionResult> ParseAsync(
        string message,
        JsonDocument? currentState = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects conflicts between extracted state and current state.
    /// </summary>
    /// <param name="extractedState">State extracted from message</param>
    /// <param name="currentState">Current game state</param>
    /// <param name="stateLastUpdatedAt">Timestamp of last state update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected conflicts</returns>
    Task<IReadOnlyList<StateConflict>> DetectConflictsAsync(
        StateExtractionResult extractedState,
        JsonDocument currentState,
        DateTime stateLastUpdatedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a JSON patch representing the state change.
    /// </summary>
    /// <param name="extraction">The extraction result</param>
    /// <param name="currentState">Current state to apply changes to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New state as JsonDocument</returns>
    Task<JsonDocument> GenerateStatePatchAsync(
        StateExtractionResult extraction,
        JsonDocument currentState,
        CancellationToken cancellationToken = default);
}

namespace Api.BoundedContexts.SessionTracking.Application.Services;

/// <summary>
/// Extracts structured game state from vision snapshot images using multimodal LLM.
/// Session Vision AI feature.
/// </summary>
internal interface IGameStateExtractor
{
    /// <summary>
    /// Extract game state from the latest snapshot for a session.
    /// Returns null if extraction fails or confidence is too low (never blocks).
    /// Idempotent: returns cached result if already extracted.
    /// </summary>
    Task<string?> ExtractIfNeededAsync(Guid sessionId, string? gameName, CancellationToken ct = default);
}

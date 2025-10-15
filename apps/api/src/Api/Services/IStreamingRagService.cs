using Api.Models;

namespace Api.Services;

/// <summary>
/// Interface for streaming RAG explain operations using Server-Sent Events (SSE)
/// API-02: Provides progressive explain responses with state updates and chunked content
/// </summary>
public interface IStreamingRagService
{
    /// <summary>
    /// Generate a streaming explain response with progressive updates
    /// Emits events: StateUpdate -> Citations -> Outline -> ScriptChunk(s) -> Complete
    /// </summary>
    /// <param name="gameId">The game identifier to explain rules for</param>
    /// <param name="topic">The topic to explain</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of streaming events</returns>
    IAsyncEnumerable<RagStreamingEvent> ExplainStreamAsync(
        string gameId,
        string topic,
        CancellationToken cancellationToken = default);
}

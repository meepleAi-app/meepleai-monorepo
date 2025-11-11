using Api.Models;

namespace Api.Services;

/// <summary>
/// CHAT-01: Interface for streaming QA operations using Server-Sent Events (SSE)
/// Provides progressive QA responses with token-by-token delivery from LLM
/// </summary>
public interface IStreamingQaService
{
    /// <summary>
    /// Generate a streaming QA response with progressive updates
    /// Emits events: StateUpdate -> Citations -> Token(s) -> Complete
    /// </summary>
    /// <param name="gameId">The game identifier to query</param>
    /// <param name="query">The user's question</param>
    /// <param name="chatId">Optional chat ID for conversation context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of streaming events</returns>
    IAsyncEnumerable<RagStreamingEvent> AskStreamAsync(
        string gameId,
        string query,
        Guid? chatId = null,
        CancellationToken cancellationToken = default);
}

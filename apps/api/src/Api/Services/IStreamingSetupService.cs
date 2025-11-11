using Api.Models;

namespace Api.Services;

/// <summary>
/// CHAT-01: Interface for streaming Setup Guide operations using Server-Sent Events (SSE)
/// Provides progressive setup guide responses with token-by-token delivery from LLM
/// </summary>
public interface IStreamingSetupService
{
    /// <summary>
    /// Generate a streaming setup guide response with progressive updates
    /// Emits events: StateUpdate -> Citations -> Token(s) -> Complete
    /// </summary>
    /// <param name="gameId">The game identifier</param>
    /// <param name="chatId">Optional chat ID for conversation context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of streaming events</returns>
    IAsyncEnumerable<RagStreamingEvent> GenerateSetupGuideStreamAsync(
        string gameId,
        Guid? chatId = null,
        CancellationToken cancellationToken = default);
}

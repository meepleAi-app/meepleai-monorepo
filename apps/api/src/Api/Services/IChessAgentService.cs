using Api.Models;

namespace Api.Services;

/// <summary>
/// CHESS-04: Chess conversational agent service
/// Specialized AI agent that answers chess questions, explains openings,
/// suggests tactics, and analyzes positions using RAG on chess knowledge base
/// </summary>
public interface IChessAgentService
{
    /// <summary>
    /// Process a chess question or position analysis request
    /// </summary>
    /// <param name="request">Chess agent request containing question and optional FEN position</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Response with answer, analysis, suggested moves, and source citations</returns>
    Task<ChessAgentResponse> AskAsync(ChessAgentRequest request, CancellationToken ct = default);
}

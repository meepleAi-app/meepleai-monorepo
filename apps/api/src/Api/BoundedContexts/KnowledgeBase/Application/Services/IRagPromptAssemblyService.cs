using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Orchestrates the full RAG prompt assembly pipeline:
/// embedding generation, vector search, chunk formatting,
/// chat history assembly, and token budget management.
/// Replaces the old AgentPromptBuilder (which only used game state).
/// </summary>
internal interface IRagPromptAssemblyService
{
    /// <summary>
    /// Assembles a complete prompt with RAG context and chat history.
    /// </summary>
    /// <param name="agentTypology">Agent type name (e.g., "tutor", "arbitro", "stratega")</param>
    /// <param name="gameTitle">Title of the game being played</param>
    /// <param name="gameState">Current game state (nullable if no active session)</param>
    /// <param name="userQuestion">The user's current question</param>
    /// <param name="gameId">Game ID for scoping Qdrant search</param>
    /// <param name="chatThread">Chat thread for history inclusion (nullable for first message)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Assembled prompt ready for LLM consumption</returns>
    Task<AssembledPrompt> AssemblePromptAsync(
        string agentTypology,
        string gameTitle,
        GameState? gameState,
        string userQuestion,
        Guid gameId,
        ChatThread? chatThread,
        CancellationToken ct);
}

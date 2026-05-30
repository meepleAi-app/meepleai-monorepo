using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;

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
    /// <param name="gameId">Game ID for scoping pgvector search</param>
    /// <param name="chatThread">Chat thread for history inclusion (nullable for first message)</param>
    /// <param name="userTier">User subscription tier for RAG enhancement routing (nullable for backward compatibility)</param>
    /// <param name="agentLanguage">Normalized ISO 639-1 language code for the agent (e.g., "it", "en"). Drives copyright instruction localization.</param>
    /// <param name="ct">Cancellation token</param>
    /// <param name="debugCollector">Optional collector for RAG debug events (null = no debug emission)</param>
    /// <param name="profileOverride">Optional retrieval profile override for debug/admin scenarios (null = use adaptive routing)</param>
    /// <returns>Assembled prompt ready for LLM consumption</returns>
    Task<AssembledPrompt> AssemblePromptAsync(
        string agentTypology,
        string gameTitle,
        GameState? gameState,
        string userQuestion,
        Guid gameId,
        ChatThread? chatThread,
        UserTier? userTier,
        string agentLanguage,
        CancellationToken ct,
        IRagDebugEventCollector? debugCollector = null,
        RetrievalProfile? profileOverride = null);

    /// <summary>
    /// Assembles a complete prompt from a pre-retrieved cross-game chunk list,
    /// bypassing the internal retrieval pipeline entirely.
    /// Designed for the cross-game SSE ask path (Task 8b, #1661) where retrieval
    /// has already been performed by <c>IMultiGameHybridSearchService</c>.
    ///
    /// When <paramref name="includeInlineCitationInstructions"/> is true, the system
    /// prompt includes a "## Citation Format" section instructing the LLM to emit
    /// [N] inline markers, and each chunk header in the RAG context is prefixed with
    /// [N] (Issue #1703 D-1703-B/C).
    /// </summary>
    /// <param name="agentTypology">Agent type name (e.g., "tutor")</param>
    /// <param name="gameTitle">Title label for the prompt context (e.g., "la tua libreria" for cross-game)</param>
    /// <param name="userQuestion">The user's current question</param>
    /// <param name="preRetrievedChunks">
    ///     Already-retrieved chunk citations. Each citation may carry a <see cref="ChunkCitation.GameId"/>
    ///     identifying its source game (populated by the cross-game retrieval path).
    /// </param>
    /// <param name="chatThread">Optional chat thread for history inclusion</param>
    /// <param name="userTier">Optional user subscription tier</param>
    /// <param name="agentLanguage">Normalized ISO 639-1 language code (e.g., "it", "en")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="includeInlineCitationInstructions">When true, prompt includes "[N]" citation format instructions and chunk headers are prefixed with [N]. Default false.</param>
    /// <returns>Assembled prompt with the pre-retrieved chunks as citations</returns>
    Task<AssembledPrompt> AssembleFromContextAsync(
        string agentTypology,
        string gameTitle,
        string userQuestion,
        IReadOnlyList<ChunkCitation> preRetrievedChunks,
        ChatThread? chatThread,
        UserTier? userTier,
        string agentLanguage,
        CancellationToken cancellationToken,
        bool includeInlineCitationInstructions = false);
}

using Api.Models;

namespace Api.Services;

/// <summary>
/// AI-04: RAG (Retrieval-Augmented Generation) service interface for question answering and explanations.
/// Provides semantic search with LLM generation and anti-hallucination guarantees.
/// </summary>
public interface IRagService
{
    /// <summary>
    /// Answer a question using RAG with LLM generation and anti-hallucination.
    /// AI-04: Core Q&A functionality
    /// AI-05: Supports caching for reduced latency
    /// PERF-03: Supports cache bypass for fresh responses
    /// AI-09: Supports multilingual queries
    /// </summary>
    /// <param name="gameId">Game ID to search within</param>
    /// <param name="query">User's question</param>
    /// <param name="language">Target language for response (default: "en")</param>
    /// <param name="bypassCache">If true, bypasses cache and forces fresh LLM response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>QA response with answer, snippets, and metadata</returns>
    Task<QaResponse> AskAsync(string gameId, string query, string? language = null, bool bypassCache = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate structured explanation with outline, script, and citations.
    /// AI-02: Core Explain functionality
    /// AI-05: Supports caching for reduced latency
    /// AI-09: Supports multilingual explanations
    /// </summary>
    /// <param name="gameId">Game ID to search within</param>
    /// <param name="topic">Topic to explain</param>
    /// <param name="language">Target language for explanation (default: "en")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Explain response with outline, script, citations, and estimated reading time</returns>
    Task<ExplainResponse> ExplainAsync(string gameId, string topic, string? language = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// AI-14: Answer a question using hybrid search (vector + keyword) with configurable search mode.
    /// Supports Semantic (vector-only), Keyword (full-text only), or Hybrid (RRF fusion).
    /// </summary>
    /// <param name="gameId">Game ID to search within</param>
    /// <param name="query">User's question</param>
    /// <param name="searchMode">Search mode: Semantic, Keyword, or Hybrid (default)</param>
    /// <param name="language">Target language for response (default: "en")</param>
    /// <param name="bypassCache">If true, bypasses cache and forces fresh LLM response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>QA response with hybrid search results</returns>
    Task<QaResponse> AskWithHybridSearchAsync(
        string gameId,
        string query,
        SearchMode searchMode = SearchMode.Hybrid,
        string? language = null,
        bool bypassCache = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ADMIN-01 Phase 4: Answer a question using a custom system prompt for evaluation purposes.
    /// This method is used exclusively by PromptEvaluationService to test different prompt versions
    /// without activating them in the database. Bypasses normal prompt retrieval.
    /// </summary>
    /// <param name="gameId">Game ID to search within</param>
    /// <param name="query">User's question</param>
    /// <param name="customSystemPrompt">Custom system prompt to use instead of configured prompt</param>
    /// <param name="searchMode">Search mode: Semantic, Keyword, or Hybrid (default)</param>
    /// <param name="language">Target language for response (default: "en")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>QA response with custom prompt applied</returns>
    Task<QaResponse> AskWithCustomPromptAsync(
        string gameId,
        string query,
        string customSystemPrompt,
        SearchMode searchMode = SearchMode.Hybrid,
        string? language = null,
        CancellationToken cancellationToken = default);
}

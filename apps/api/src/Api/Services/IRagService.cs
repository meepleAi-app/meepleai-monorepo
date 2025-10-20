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
}

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
    /// </summary>
    /// <param name="gameId">Game ID to search within</param>
    /// <param name="query">User's question</param>
    /// <param name="bypassCache">If true, bypasses cache and forces fresh LLM response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>QA response with answer, snippets, and metadata</returns>
    Task<QaResponse> AskAsync(string gameId, string query, bool bypassCache = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate structured explanation with outline, script, and citations.
    /// AI-02: Core Explain functionality
    /// AI-05: Supports caching for reduced latency
    /// </summary>
    /// <param name="gameId">Game ID to search within</param>
    /// <param name="topic">Topic to explain</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Explain response with outline, script, citations, and estimated reading time</returns>
    Task<ExplainResponse> ExplainAsync(string gameId, string topic, CancellationToken cancellationToken = default);
}

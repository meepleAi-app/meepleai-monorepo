using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;

/// <summary>
/// Classifies user queries by complexity to determine optimal RAG routing.
/// Uses a two-tier approach: fast heuristics first, LLM fallback for ambiguous cases.
/// </summary>
internal interface IQueryComplexityClassifier
{
    /// <summary>
    /// Classify the complexity of a user query for adaptive RAG routing.
    /// </summary>
    /// <param name="query">The user's natural language query</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Classification result with level, confidence, and reason</returns>
    Task<QueryComplexity> ClassifyAsync(string query, CancellationToken ct = default);
}

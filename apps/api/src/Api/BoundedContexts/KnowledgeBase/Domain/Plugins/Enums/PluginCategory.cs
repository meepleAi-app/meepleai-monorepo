// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3414 - Plugin Contract & Interfaces
// =============================================================================

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;

/// <summary>
/// Defines the functional categories for RAG pipeline plugins.
/// Each category represents a distinct stage or function in the RAG workflow.
/// </summary>
public enum PluginCategory
{
    /// <summary>
    /// Routing plugins determine which path a query should take through the pipeline.
    /// Examples: Intent detection, complexity routing, tier-based routing.
    /// </summary>
    Routing = 0,

    /// <summary>
    /// Cache plugins handle caching of queries, embeddings, and results.
    /// Examples: Semantic cache, exact match cache, result cache.
    /// </summary>
    Cache = 1,

    /// <summary>
    /// Retrieval plugins fetch relevant documents from knowledge sources.
    /// Examples: Vector search, keyword search, hybrid retrieval.
    /// </summary>
    Retrieval = 2,

    /// <summary>
    /// Evaluation plugins assess quality of retrieved documents or generated responses.
    /// Examples: Relevance scoring, confidence calculation, citation validation.
    /// </summary>
    Evaluation = 3,

    /// <summary>
    /// Generation plugins create responses using LLMs or other generative models.
    /// Examples: Answer generation, summary generation, explanation generation.
    /// </summary>
    Generation = 4,

    /// <summary>
    /// Validation plugins verify correctness and safety of inputs/outputs.
    /// Examples: Input sanitization, output validation, guardrails.
    /// </summary>
    Validation = 5,

    /// <summary>
    /// Transform plugins modify data between pipeline stages.
    /// Examples: Query rewriting, document reranking, result formatting.
    /// </summary>
    Transform = 6,

    /// <summary>
    /// Filter plugins remove or select data based on criteria.
    /// Examples: Document filtering, result deduplication, relevance thresholding.
    /// </summary>
    Filter = 7
}

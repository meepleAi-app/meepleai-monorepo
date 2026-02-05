// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3420 - Retrieval Plugins
// =============================================================================

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Retrieval;

/// <summary>
/// Represents a document retrieved from a knowledge source.
/// </summary>
public sealed record RetrievedDocument
{
    /// <summary>
    /// Unique identifier for the document.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The text content of the document.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Relevance score (0-1).
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    /// Source identifier (e.g., "rules-kb", "faq-kb", "game-docs").
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Additional metadata about the document.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

    /// <summary>
    /// Creates a new retrieved document.
    /// </summary>
    public static RetrievedDocument Create(
        string id,
        string content,
        double score,
        string source,
        Dictionary<string, object>? metadata = null)
    {
        return new RetrievedDocument
        {
            Id = id,
            Content = content,
            Score = score,
            Source = source,
            Metadata = metadata ?? new Dictionary<string, object>(StringComparer.Ordinal)
        };
    }
}

/// <summary>
/// Metrics from a retrieval operation.
/// </summary>
public sealed record RetrievalMetrics
{
    /// <summary>
    /// Number of vector search hits.
    /// </summary>
    public int VectorHits { get; init; }

    /// <summary>
    /// Number of keyword search hits.
    /// </summary>
    public int KeywordHits { get; init; }

    /// <summary>
    /// Time spent on fusion in milliseconds.
    /// </summary>
    public double FusionTimeMs { get; init; }

    /// <summary>
    /// Total retrieval time in milliseconds.
    /// </summary>
    public double TotalTimeMs { get; init; }

    /// <summary>
    /// Number of sources searched.
    /// </summary>
    public int SourcesSearched { get; init; }
}

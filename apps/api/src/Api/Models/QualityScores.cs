namespace Api.Models;

/// <summary>
/// Multi-dimensional quality scores for an AI response.
/// Provides granular metrics for RAG quality, LLM quality, citation quality, and overall confidence.
/// </summary>
public class QualityScores
{
    /// <summary>
    /// RAG confidence score based on semantic search result scores.
    /// Range: 0.0 to 1.0
    /// Calculation: Average of RAG search result scores, or 0.0 if no results.
    /// </summary>
    public double RagConfidence { get; set; }

    /// <summary>
    /// LLM confidence score based on response characteristics.
    /// Range: 0.0 to 1.0
    /// Heuristic-based (unless model reports confidence explicitly):
    /// - Very short responses (&lt;50 words): -0.3 penalty
    /// - Short responses (&lt;100 words): -0.15 penalty
    /// - Hedging phrases: -0.05 per phrase
    /// - Baseline: 0.85, capped at [0.0, 1.0]
    /// </summary>
    public double LlmConfidence { get; set; }

    /// <summary>
    /// Citation quality score based on citation-to-paragraph ratio.
    /// Range: 0.0 to 1.0
    /// Calculation: Min(citations.Count / paragraphCount, 1.0)
    /// </summary>
    public double CitationQuality { get; set; }

    /// <summary>
    /// Overall confidence score (weighted average of all dimensions).
    /// Range: 0.0 to 1.0
    /// Calculation: (RAG * 0.40) + (LLM * 0.40) + (Citation * 0.20)
    /// </summary>
    public double OverallConfidence { get; set; }

    /// <summary>
    /// Indicates whether this response is flagged as low-quality.
    /// Threshold: OverallConfidence &lt; 0.60 (exclusive)
    /// </summary>
    public bool IsLowQuality { get; set; }
}

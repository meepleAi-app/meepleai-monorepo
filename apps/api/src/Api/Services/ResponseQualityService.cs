using Api.Models;

namespace Api.Services;

/// <summary>
/// Service for calculating multi-dimensional quality scores for AI responses.
/// AI-11: Provides RAG confidence, LLM confidence, citation quality, and overall confidence metrics.
/// </summary>
public class ResponseQualityService : IResponseQualityService
{
    // Heuristic constants for LLM confidence calculation
    private const double BaseConfidence = 0.85;
    private const double VeryShortPenalty = 0.30;  // <50 words
    private const double ShortPenalty = 0.15;      // <100 words
    private const double HedgingPhrasePenalty = 0.05;
    private const int VeryShortThreshold = 50;
    private const int ShortThreshold = 100;

    // Low-quality threshold (exclusive: < 0.60 is low-quality)
    private const double LowQualityThreshold = 0.60;

    // Weighted average weights for overall confidence
    private const double RagWeight = 0.40;
    private const double LlmWeight = 0.40;
    private const double CitationWeight = 0.20;

    // Hedging phrases that reduce LLM confidence
    private static readonly string[] HedgingPhrases =
    {
        "might", "possibly", "unclear", "I'm not sure", "maybe", "perhaps", "I think"
    };

    /// <summary>
    /// Calculate quality scores for an AI response.
    /// </summary>
    /// <param name="ragResults">RAG search results with confidence scores</param>
    /// <param name="citations">Citations included in the response</param>
    /// <param name="responseText">The generated response text</param>
    /// <param name="modelReportedConfidence">Optional confidence score reported by the LLM model</param>
    /// <returns>Multi-dimensional quality scores</returns>
    public QualityScores CalculateQualityScores(
        List<RagSearchResult> ragResults,
        List<Citation>? citations,
        string? responseText,
        double? modelReportedConfidence = null)
    {
        // Calculate RAG confidence (average of search scores)
        var ragConfidence = CalculateRagConfidence(ragResults);

        // Calculate LLM confidence (heuristic-based or model-reported)
        var llmConfidence = modelReportedConfidence ?? CalculateLlmConfidence(responseText);

        // Calculate citation quality (citations-to-paragraphs ratio)
        var citationQuality = CalculateCitationQuality(citations, responseText);

        // Calculate overall confidence (weighted average)
        var overallConfidence = CalculateOverallConfidence(ragConfidence, llmConfidence, citationQuality);

        // Determine if low-quality (exclusive threshold)
        var isLowQuality = overallConfidence < LowQualityThreshold;

        return new QualityScores
        {
            RagConfidence = ragConfidence,
            LlmConfidence = llmConfidence,
            CitationQuality = citationQuality,
            OverallConfidence = overallConfidence,
            IsLowQuality = isLowQuality
        };
    }

    /// <summary>
    /// Calculate RAG confidence as average of search result scores.
    /// Returns 0.0 if no results.
    /// </summary>
    private static double CalculateRagConfidence(List<RagSearchResult> ragResults)
    {
        if (ragResults == null || ragResults.Count == 0)
        {
            return 0.0;
        }

        return ragResults.Average(r => r.Score);
    }

    /// <summary>
    /// Calculate LLM confidence using heuristics based on response characteristics.
    /// Penalizes very short responses, short responses, and hedging phrases.
    /// </summary>
    private static double CalculateLlmConfidence(string? responseText)
    {
        // Empty or null response gets 0.0
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return 0.0;
        }

        var confidence = BaseConfidence;

        // Count words in response
        var words = responseText.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var wordCount = words.Length;

        // Apply length penalties
        if (wordCount < VeryShortThreshold)
        {
            confidence -= VeryShortPenalty;
        }
        else if (wordCount < ShortThreshold)
        {
            confidence -= ShortPenalty;
        }

        // Count hedging phrases (case-insensitive)
        var lowerText = responseText.ToLowerInvariant();
        foreach (var phrase in HedgingPhrases)
        {
            if (lowerText.Contains(phrase.ToLowerInvariant()))
            {
                confidence -= HedgingPhrasePenalty;
            }
        }

        // Cap at [0.0, 1.0]
        return Math.Max(0.0, Math.Min(1.0, confidence));
    }

    /// <summary>
    /// Calculate citation quality as ratio of citations to paragraphs.
    /// Capped at 1.0 (no penalty for over-citing).
    /// </summary>
    private static double CalculateCitationQuality(List<Citation>? citations, string? responseText)
    {
        // Null citations list gets 0.0
        if (citations == null)
        {
            return 0.0;
        }

        // Empty response gets 0.0
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return 0.0;
        }

        // Count paragraphs (split by double newlines or single newlines)
        var paragraphs = responseText
            .Split(new[] { "\n\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        var paragraphCount = Math.Max(1, paragraphs.Length);
        var citationCount = citations.Count;

        // Calculate ratio and cap at 1.0
        return Math.Min(citationCount / (double)paragraphCount, 1.0);
    }

    /// <summary>
    /// Calculate overall confidence as weighted average of all dimensions.
    /// Weights: RAG 40%, LLM 40%, Citation 20%
    /// </summary>
    private static double CalculateOverallConfidence(double ragConfidence, double llmConfidence, double citationQuality)
    {
        return (ragConfidence * RagWeight) + (llmConfidence * LlmWeight) + (citationQuality * CitationWeight);
    }
}

/// <summary>
/// Interface for response quality service.
/// </summary>
public interface IResponseQualityService
{
    /// <summary>
    /// Calculate quality scores for an AI response.
    /// </summary>
    QualityScores CalculateQualityScores(
        List<RagSearchResult> ragResults,
        List<Citation>? citations,
        string? responseText,
        double? modelReportedConfidence = null);
}

/// <summary>
/// RAG search result with confidence score.
/// Used for quality scoring calculations.
/// </summary>
public class RagSearchResult
{
    public double Score { get; set; }
    // Additional properties can be added as needed
}

/// <summary>
/// Citation reference in an AI response.
/// Links response content back to source documents.
/// </summary>
public class Citation
{
    public Guid DocumentId { get; set; }
    public int PageNumber { get; set; }
    public string SnippetText { get; set; } = string.Empty;
}

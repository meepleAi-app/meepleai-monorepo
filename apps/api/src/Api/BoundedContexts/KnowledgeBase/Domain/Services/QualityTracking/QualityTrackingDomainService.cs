using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for RAG quality tracking and confidence scoring.
/// Determines if responses meet quality thresholds.
/// </summary>
public class QualityTrackingDomainService
{
    private const double LowQualityThreshold = 0.5;
    private const double HighQualityThreshold = 0.8;

    /// <summary>
    /// Calculates overall confidence score from search results.
    /// Uses average of top results weighted by rank.
    /// </summary>
    public virtual Confidence CalculateSearchConfidence(List<SearchResult> results)
    {
        if (results == null || results.Count == 0)
            return Confidence.Zero;

        // Weight results by rank (top results have more weight)
        var weightedSum = 0.0;
        var totalWeight = 0.0;

        foreach (var result in results.Take(5)) // Top 5 results
        {
            var weight = 1.0 / result.Rank; // Higher rank = lower weight
            weightedSum += result.RelevanceScore.Value * weight;
            totalWeight += weight;
        }

        var avgScore = totalWeight > 0 ? weightedSum / totalWeight : 0.0;
        return new Confidence(avgScore);
    }

    /// <summary>
    /// Calculates LLM response confidence based on response characteristics.
    /// </summary>
    public virtual Confidence CalculateLlmConfidence(
        string llmResponse,
        List<SearchResult> sourceResults)
    {
        if (string.IsNullOrWhiteSpace(llmResponse))
            return Confidence.Zero;

        var confidence = 0.5; // Base confidence

        // Increase confidence if response references sources
        if (HasCitations(llmResponse))
            confidence += 0.2;

        // Increase confidence if sources are high quality
        var searchConfidence = CalculateSearchConfidence(sourceResults);
        confidence += searchConfidence.Value * 0.3;

        // Cap at 1.0
        confidence = Math.Min(confidence, 1.0);

        return new Confidence(confidence);
    }

    /// <summary>
    /// Calculates overall RAG confidence combining search and LLM confidence.
    /// </summary>
    public virtual Confidence CalculateOverallConfidence(
        Confidence searchConfidence,
        Confidence llmConfidence)
    {
        // Weighted average: 70% search, 30% LLM
        var overall = (searchConfidence.Value * 0.7) + (llmConfidence.Value * 0.3);
        return new Confidence(overall);
    }

    /// <summary>
    /// Determines if response meets quality threshold.
    /// </summary>
    public virtual bool IsLowQuality(Confidence overallConfidence)
    {
        return overallConfidence.Value < LowQualityThreshold;
    }

    /// <summary>
    /// Determines if response is high quality.
    /// </summary>
    public virtual bool IsHighQuality(Confidence overallConfidence)
    {
        return overallConfidence.Value >= HighQualityThreshold;
    }

    /// <summary>
    /// Checks if LLM response contains citation markers.
    /// </summary>
    private bool HasCitations(string response)
    {
        // Check for common citation patterns: [1], (Page 5), etc.
        return response.Contains("[") || response.Contains("(Page");
    }

    /// <summary>
    /// Calculates citation quality score.
    /// </summary>
    public virtual Confidence CalculateCitationQuality(
        List<Citation> citations,
        string llmResponse)
    {
        if (citations == null || citations.Count == 0)
            return Confidence.Zero;

        // Check if citations are actually referenced in response
        var referencedCount = citations.Count(c =>
            llmResponse.Contains($"[{c.PageNumber}]") ||
            llmResponse.Contains($"Page {c.PageNumber}"));

        var citationAccuracy = referencedCount / (double)citations.Count;
        return new Confidence(citationAccuracy);
    }
}

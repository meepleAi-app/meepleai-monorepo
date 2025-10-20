// AI-11: Quality Scoring Metrics for OpenTelemetry
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Api.Models;

namespace Api.Observability;

/// <summary>
/// Provides OpenTelemetry metrics for AI response quality tracking.
/// Records multi-dimensional quality scores and low-quality response counts.
/// </summary>
public class QualityMetrics
{
    private readonly Histogram<double> _qualityScoreHistogram;
    private readonly Counter<long> _lowQualityCounter;

    /// <summary>
    /// Quality tier thresholds for classification.
    /// </summary>
    private const double HighQualityThreshold = 0.80;
    private const double MediumQualityThreshold = 0.60;

    public QualityMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("MeepleAI.Api", "1.0.0");

        _qualityScoreHistogram = meter.CreateHistogram<double>(
            name: "meepleai.quality.score",
            unit: "score",
            description: "Quality score distribution across dimensions (RAG, LLM, Citation, Overall)");

        _lowQualityCounter = meter.CreateCounter<long>(
            name: "meepleai.quality.low_quality_responses.total",
            unit: "responses",
            description: "Total number of low-quality AI responses");
    }

    /// <summary>
    /// Record quality scores for an AI response with dimension-specific metrics.
    /// </summary>
    /// <param name="scores">Quality scores to record</param>
    /// <param name="agentType">Type of agent (e.g., "qa", "explain", "setup")</param>
    /// <param name="operation">Operation performed (e.g., "answer", "generate", "stream")</param>
    public void RecordQualityScores(QualityScores scores, string agentType, string operation)
    {
        var qualityTier = DetermineQualityTier(scores.OverallConfidence);

        // Record histogram for each dimension with common tags
        // Record histogram for each dimension with unique tags
        var baseTags = new TagList
        {
            { "agent.type", agentType },
            { "operation", operation },
            { "quality_tier", qualityTier }
        };

        var ragTags = baseTags;
        ragTags.Add("dimension", "rag_confidence");
        _qualityScoreHistogram.Record(scores.RagConfidence, ragTags);

        var llmTags = baseTags;
        llmTags.Add("dimension", "llm_confidence");
        _qualityScoreHistogram.Record(scores.LlmConfidence, llmTags);

        var citationTags = baseTags;
        citationTags.Add("dimension", "citation_quality");
        _qualityScoreHistogram.Record(scores.CitationQuality, citationTags);

        var overallTags = baseTags;
        overallTags.Add("dimension", "overall_confidence");
        _qualityScoreHistogram.Record(scores.OverallConfidence, overallTags);

        // Increment low-quality counter if flagged
        if (scores.IsLowQuality)
        {
            var counterTags = new TagList
            {
                { "agent.type", agentType },
                { "operation", operation }
            };
            _lowQualityCounter.Add(1, counterTags);
        }
    }

    /// <summary>
    /// Determine quality tier based on overall confidence score.
    /// - high: >= 0.80
    /// - medium: >= 0.60 and < 0.80
    /// - low: < 0.60
    /// </summary>
    private string DetermineQualityTier(double overallConfidence)
    {
        if (overallConfidence >= HighQualityThreshold)
        {
            return "high";
        }
        else if (overallConfidence >= MediumQualityThreshold)
        {
            return "medium";
        }
        else
        {
            return "low";
        }
    }
}


namespace Api.Models;

/// <summary>
/// Quality report for AI responses over a specified time period.
/// Contains aggregated statistics about response quality metrics.
/// </summary>
internal class QualityReport
{
    /// <summary>
    /// Start date of the reporting period (inclusive).
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date of the reporting period (inclusive).
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Total number of AI responses in the period.
    /// </summary>
    public int TotalResponses { get; set; }

    /// <summary>
    /// Number of low-quality responses (OverallConfidence &lt; 0.60).
    /// </summary>
    public int LowQualityCount { get; set; }

    /// <summary>
    /// Percentage of low-quality responses (0-100).
    /// </summary>
    public double LowQualityPercentage { get; set; }

    /// <summary>
    /// Average RAG confidence across all responses.
    /// Null if no responses in period.
    /// </summary>
    public double? AverageRagConfidence { get; set; }

    /// <summary>
    /// Average LLM confidence across all responses.
    /// Null if no responses in period.
    /// </summary>
    public double? AverageLlmConfidence { get; set; }

    /// <summary>
    /// Average citation quality across all responses.
    /// Null if no responses in period.
    /// </summary>
    public double? AverageCitationQuality { get; set; }

    /// <summary>
    /// Average overall confidence across all responses.
    /// Null if no responses in period.
    /// </summary>
    public double? AverageOverallConfidence { get; set; }
}

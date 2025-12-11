using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.BoundedContexts.KnowledgeBase.Domain.GridSearch;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.KnowledgeBase.Domain.Reports;

/// <summary>
/// ADR-016 Phase 5: Benchmark report for RAG pipeline evaluation.
/// Contains structured data for generating markdown reports.
/// </summary>
public sealed record BenchmarkReport
{
    /// <summary>
    /// Report generation timestamp.
    /// </summary>
    public required DateTime GeneratedAt { get; init; }

    /// <summary>
    /// Title of the evaluation report.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Dataset used for evaluation.
    /// </summary>
    public required string DatasetName { get; init; }

    /// <summary>
    /// Number of samples evaluated.
    /// </summary>
    public required int SampleCount { get; init; }

    /// <summary>
    /// Grid search results containing all configuration evaluations.
    /// </summary>
    public required GridSearchResult GridSearchResult { get; init; }

    /// <summary>
    /// Phase 5 target metrics for comparison.
    /// </summary>
    public Phase5Targets Targets { get; init; } = Phase5Targets.Default;

    /// <summary>
    /// Summary of the evaluation results.
    /// </summary>
    public ReportSummary Summary => ComputeSummary();

    /// <summary>
    /// Creates a benchmark report from grid search results.
    /// </summary>
    public static BenchmarkReport FromGridSearchResult(GridSearchResult result)
    {
        var sampleCount = result.ConfigurationResults
            .Where(r => r.IsSuccess)
            .Select(r => r.SampleCount)
            .DefaultIfEmpty(0)
            .Max();

        return new BenchmarkReport
        {
            GeneratedAt = DateTime.UtcNow,
            Title = $"RAG Pipeline Evaluation Report - {result.DatasetName}",
            DatasetName = result.DatasetName,
            SampleCount = sampleCount,
            GridSearchResult = result
        };
    }

    private ReportSummary ComputeSummary()
    {
        var successfulConfigs = GridSearchResult.ConfigurationResults
            .Where(r => r.IsSuccess)
            .ToList();

        var meetingTargets = successfulConfigs
            .Where(r => r.Metrics.MeetsPhase5Target())
            .ToList();

        return new ReportSummary
        {
            TotalConfigurations = GridSearchResult.ConfigurationCount,
            SuccessfulConfigurations = successfulConfigs.Count,
            ConfigurationsMeetingTarget = meetingTargets.Count,
            BestRecallAt10 = successfulConfigs.Select(r => r.Metrics.RecallAt10).DefaultIfEmpty(0).Max(),
            BestNdcgAt10 = successfulConfigs.Select(r => r.Metrics.NdcgAt10).DefaultIfEmpty(0).Max(),
            BestP95LatencyMs = successfulConfigs.Select(r => r.Metrics.P95LatencyMs).DefaultIfEmpty(0).Min(),
            MeetsPhase5Target = meetingTargets.Count > 0,
            RecommendedConfiguration = DetermineRecommendedConfiguration(successfulConfigs)
        };
    }

    private static string? DetermineRecommendedConfiguration(
        IReadOnlyList<ConfigurationResult> results)
    {
        if (results.Count == 0) return null;

        // Prefer configurations meeting target, then best balance of recall and latency
        var meetingTarget = results
            .Where(r => r.Metrics.MeetsPhase5Target())
            .OrderByDescending(r => r.Metrics.RecallAt10)
            .ThenBy(r => r.Metrics.P95LatencyMs)
            .FirstOrDefault();

        if (meetingTarget != null)
            return meetingTarget.Configuration.ConfigurationId;

        // Fallback: best recall regardless of latency
        return results
            .OrderByDescending(r => r.Metrics.RecallAt10)
            .First()
            .Configuration.ConfigurationId;
    }
}

/// <summary>
/// Phase 5 target metrics for evaluation.
/// </summary>
public sealed record Phase5Targets
{
    /// <summary>
    /// Minimum Recall@10 target.
    /// </summary>
    public double MinRecallAt10 { get; init; } = 0.70;

    /// <summary>
    /// Maximum P95 latency in milliseconds.
    /// </summary>
    public double MaxP95LatencyMs { get; init; } = 1500.0;

    /// <summary>
    /// Default Phase 5 targets from ADR-016.
    /// </summary>
    public static Phase5Targets Default => new();
}

/// <summary>
/// Summary statistics for the benchmark report.
/// </summary>
public sealed record ReportSummary
{
    /// <summary>
    /// Total configurations evaluated.
    /// </summary>
    public required int TotalConfigurations { get; init; }

    /// <summary>
    /// Configurations that completed successfully.
    /// </summary>
    public required int SuccessfulConfigurations { get; init; }

    /// <summary>
    /// Configurations meeting Phase 5 targets.
    /// </summary>
    public required int ConfigurationsMeetingTarget { get; init; }

    /// <summary>
    /// Best Recall@10 achieved.
    /// </summary>
    public required double BestRecallAt10 { get; init; }

    /// <summary>
    /// Best nDCG@10 achieved.
    /// </summary>
    public required double BestNdcgAt10 { get; init; }

    /// <summary>
    /// Best (lowest) P95 latency achieved.
    /// </summary>
    public required double BestP95LatencyMs { get; init; }

    /// <summary>
    /// Whether any configuration meets Phase 5 target.
    /// </summary>
    public required bool MeetsPhase5Target { get; init; }

    /// <summary>
    /// Recommended configuration based on analysis.
    /// </summary>
    public string? RecommendedConfiguration { get; init; }
}

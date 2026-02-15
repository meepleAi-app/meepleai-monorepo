namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Aggregated beta testing metrics for Arbitro Agent.
/// Issue #4328: Arbitro Agent Beta Testing and Monitoring.
/// </summary>
public record ArbitroBetaMetricsDto
{
    /// <summary>
    /// Total feedback submissions received.
    /// </summary>
    public required int TotalFeedback { get; init; }

    /// <summary>
    /// Overall accuracy: % of feedbacks marked as "Correct".
    /// Target: >90%
    /// </summary>
    public required double AccuracyPercentage { get; init; }

    /// <summary>
    /// Conflict resolution accuracy: % correct in cases with conflicts.
    /// Target: >85%
    /// </summary>
    public required double ConflictResolutionAccuracy { get; init; }

    /// <summary>
    /// Average user satisfaction rating (1-5 scale).
    /// Target: >4.0
    /// </summary>
    public required double AverageRating { get; init; }

    /// <summary>
    /// Decision distribution breakdown.
    /// </summary>
    public required DecisionDistributionDto DecisionDistribution { get; init; }

    /// <summary>
    /// FAQ fast-path statistics.
    /// </summary>
    public required FaqStatisticsDto FaqStatistics { get; init; }

    /// <summary>
    /// Most common violated rules (for failure pattern analysis).
    /// </summary>
    public required List<ViolatedRuleStatsDto> TopViolatedRules { get; init; }

    /// <summary>
    /// Accuracy trend over time (daily aggregates).
    /// </summary>
    public required List<AccuracyTrendPointDto> AccuracyTrend { get; init; }
}

/// <summary>
/// Distribution of AI decisions.
/// </summary>
public record DecisionDistributionDto
{
    public required int ValidCount { get; init; }
    public required int InvalidCount { get; init; }
    public required int UncertainCount { get; init; }
    public required double ValidPercentage { get; init; }
    public required double InvalidPercentage { get; init; }
    public required double UncertainPercentage { get; init; }
}

/// <summary>
/// FAQ fast-path usage statistics.
/// </summary>
public record FaqStatisticsDto
{
    public required int TotalFaqHits { get; init; }
    public required int TotalLlmCalls { get; init; }
    public required double FaqHitRate { get; init; }
    public required List<TopFaqEntryDto> TopFaqEntries { get; init; }
}

/// <summary>
/// FAQ entry usage ranking.
/// </summary>
public record TopFaqEntryDto
{
    public required Guid FaqId { get; init; }
    public required string Pattern { get; init; }
    public required int UsageCount { get; init; }
    public required double SuccessRate { get; init; }
}

/// <summary>
/// Violated rule frequency statistics.
/// </summary>
public record ViolatedRuleStatsDto
{
    public required string RuleId { get; init; }
    public required int ViolationCount { get; init; }
    public required double PercentageOfTotal { get; init; }
}

/// <summary>
/// Daily accuracy trend point.
/// </summary>
public record AccuracyTrendPointDto
{
    public required DateTime Date { get; init; }
    public required int FeedbackCount { get; init; }
    public required double AccuracyPercentage { get; init; }
    public required double AverageConfidence { get; init; }
}

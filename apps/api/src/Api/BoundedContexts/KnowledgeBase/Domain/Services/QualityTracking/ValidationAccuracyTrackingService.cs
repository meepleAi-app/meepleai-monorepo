using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for tracking and calculating validation accuracy metrics.
/// BGAI-039: Measures how accurately the validation system identifies correct vs. incorrect responses.
/// Target: >= 80% accuracy baseline
/// </summary>
internal class ValidationAccuracyTrackingService
{
    /// <summary>
    /// Minimum accuracy threshold for validation system (80%)
    /// </summary>
    public const double MinimumAccuracyThreshold = 0.80;

    /// <summary>
    /// Calculates validation accuracy metrics from evaluation results.
    /// Compares expected outcomes (from test cases) against actual validation results.
    /// </summary>
    /// <param name="evaluationResult">The prompt evaluation result to analyze</param>
    /// <param name="expectedValidCount">Expected number of valid responses (ground truth)</param>
    /// <returns>ValidationAccuracyMetrics with precision, recall, F1, and accuracy</returns>
    public ValidationAccuracyMetrics CalculateAccuracyMetrics(
        PromptEvaluationResult evaluationResult,
        int expectedValidCount)
    {
        ArgumentNullException.ThrowIfNull(evaluationResult);

        if (expectedValidCount < 0 || expectedValidCount > evaluationResult.TotalQueries)
            throw new ArgumentException(
                $"Expected valid count must be between 0 and {evaluationResult.TotalQueries}",
                nameof(expectedValidCount));

        var expectedInvalidCount = evaluationResult.TotalQueries - expectedValidCount;

        // Count how many responses passed each validation metric
        // For this baseline, we consider a response "valid" if it meets ALL 5 metrics
        var actualValidCount = evaluationResult.QueryResults.Count(r =>
            r.IsAccurate && r.IsRelevant && r.IsComplete && r.IsClear && r.HasGoodCitationQuality);

        var actualInvalidCount = evaluationResult.TotalQueries - actualValidCount;

        // Calculate confusion matrix
        // True Positives: Expected valid AND actually valid
        var truePositives = Math.Min(expectedValidCount, actualValidCount);

        // True Negatives: Expected invalid AND actually invalid
        var trueNegatives = Math.Min(expectedInvalidCount, actualInvalidCount);

        // False Positives: Expected invalid BUT actually valid
        var falsePositives = Math.Max(0, actualValidCount - expectedValidCount);

        // False Negatives: Expected valid BUT actually invalid
        var falseNegatives = Math.Max(0, expectedValidCount - actualValidCount);

        return ValidationAccuracyMetrics.Create(
            truePositives,
            trueNegatives,
            falsePositives,
            falseNegatives);
    }

    /// <summary>
    /// Calculates validation accuracy for a specific metric dimension.
    /// Allows measuring accuracy of individual validation layers (e.g., just accuracy, just relevance).
    /// </summary>
    /// <param name="evaluationResult">The prompt evaluation result to analyze</param>
    /// <param name="expectedValidCount">Expected number of valid responses for this metric</param>
    /// <param name="metricSelector">Function to select which metric to evaluate</param>
    /// <returns>ValidationAccuracyMetrics for the specific dimension</returns>
    public ValidationAccuracyMetrics CalculateMetricDimensionAccuracy(
        PromptEvaluationResult evaluationResult,
        int expectedValidCount,
        Func<QueryEvaluationResult, bool> metricSelector)
    {
        ArgumentNullException.ThrowIfNull(evaluationResult);
        ArgumentNullException.ThrowIfNull(metricSelector);

        if (expectedValidCount < 0 || expectedValidCount > evaluationResult.TotalQueries)
            throw new ArgumentException(
                $"Expected valid count must be between 0 and {evaluationResult.TotalQueries}",
                nameof(expectedValidCount));

        var expectedInvalidCount = evaluationResult.TotalQueries - expectedValidCount;

        // Count actual valid/invalid for this specific metric
        var actualValidCount = evaluationResult.QueryResults.Count(metricSelector);
        var actualInvalidCount = evaluationResult.TotalQueries - actualValidCount;

        // Calculate confusion matrix
        var truePositives = Math.Min(expectedValidCount, actualValidCount);
        var trueNegatives = Math.Min(expectedInvalidCount, actualInvalidCount);
        var falsePositives = Math.Max(0, actualValidCount - expectedValidCount);
        var falseNegatives = Math.Max(0, expectedValidCount - actualValidCount);

        return ValidationAccuracyMetrics.Create(
            truePositives,
            trueNegatives,
            falsePositives,
            falseNegatives);
    }

    /// <summary>
    /// Calculates aggregated validation accuracy across multiple evaluation results.
    /// Useful for measuring baseline accuracy across a full test suite.
    /// </summary>
    /// <param name="evaluationResults">List of evaluation results to aggregate</param>
    /// <param name="expectedValidCounts">Expected valid counts for each evaluation (must match length)</param>
    /// <returns>Aggregated ValidationAccuracyMetrics</returns>
    public ValidationAccuracyMetrics CalculateAggregatedAccuracy(
        IReadOnlyList<PromptEvaluationResult> evaluationResults,
        IReadOnlyList<int> expectedValidCounts)
    {
        if (evaluationResults == null || evaluationResults.Count == 0)
            throw new ArgumentException("Evaluation results cannot be null or empty", nameof(evaluationResults));

        if (expectedValidCounts == null || expectedValidCounts.Count != evaluationResults.Count)
            throw new ArgumentException(
                "Expected valid counts must match evaluation results count",
                nameof(expectedValidCounts));

        var totalTp = 0;
        var totalTn = 0;
        var totalFp = 0;
        var totalFn = 0;

        for (var i = 0; i < evaluationResults.Count; i++)
        {
            var metrics = CalculateAccuracyMetrics(evaluationResults[i], expectedValidCounts[i]);
            totalTp += metrics.TruePositives;
            totalTn += metrics.TrueNegatives;
            totalFp += metrics.FalsePositives;
            totalFn += metrics.FalseNegatives;
        }

        return ValidationAccuracyMetrics.Create(totalTp, totalTn, totalFp, totalFn);
    }

    /// <summary>
    /// Generates a detailed accuracy report with recommendations.
    /// </summary>
    /// <param name="metrics">Validation accuracy metrics to report on</param>
    /// <param name="context">Context description (e.g., "Overall Validation", "Layer 1: Confidence")</param>
    /// <returns>Human-readable accuracy report</returns>
    public ValidationAccuracyReport GenerateAccuracyReport(
        ValidationAccuracyMetrics metrics,
        string context)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        if (string.IsNullOrWhiteSpace(context))
            throw new ArgumentException("Context cannot be null or whitespace", nameof(context));

        var recommendations = GenerateRecommendations(metrics);
        var summary = GenerateSummary(metrics, context);

        return new ValidationAccuracyReport(
            Context: context,
            Metrics: metrics,
            MeetsBaseline: metrics.MeetsBaselineThreshold,
            QualityLevel: metrics.QualityLevel,
            Summary: summary,
            Recommendations: recommendations,
            Timestamp: DateTime.UtcNow);
    }

    /// <summary>
    /// Generates actionable recommendations based on metrics.
    /// </summary>
    private static IReadOnlyList<string> GenerateRecommendations(ValidationAccuracyMetrics metrics)
    {
        var recommendations = new List<string>();

        if (metrics.Accuracy < MinimumAccuracyThreshold)
        {
            recommendations.Add($"⚠️ Accuracy below baseline threshold ({metrics.Accuracy:P2} < {MinimumAccuracyThreshold:P0}). Investigation required.");
        }

        if (metrics.Precision < 0.75)
        {
            recommendations.Add($"🔍 Low precision ({metrics.Precision:P2}) indicates high false positive rate. Review validation criteria to reduce false alarms.");
        }

        if (metrics.Recall < 0.75)
        {
            recommendations.Add($"🔍 Low recall ({metrics.Recall:P2}) indicates high false negative rate. Validation may be too strict, missing valid responses.");
        }

        if (metrics.FalsePositives > 0 && metrics.FalsePositives > metrics.FalseNegatives * 2)
        {
            recommendations.Add("⚠️ High false positive count. Validation system may be too permissive. Tighten thresholds.");
        }

        if (metrics.FalseNegatives > 0 && metrics.FalseNegatives > metrics.FalsePositives * 2)
        {
            recommendations.Add("⚠️ High false negative count. Validation system may be too strict. Consider relaxing thresholds.");
        }

        if (metrics.F1Score < 0.80)
        {
            recommendations.Add($"📊 F1-Score below optimal ({metrics.F1Score:P2}). Balance precision and recall improvements.");
        }

        if (metrics.MatthewsCorrelationCoefficient < 0.60)
        {
            recommendations.Add($"📉 Matthews Correlation Coefficient low ({metrics.MatthewsCorrelationCoefficient:F3}). Validation may not be better than random for this dataset.");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add($"✅ Validation system performing well. Accuracy: {metrics.Accuracy:P2}, F1: {metrics.F1Score:P2}. Continue monitoring.");
        }

        return recommendations;
    }

    /// <summary>
    /// Generates a summary message for the metrics.
    /// </summary>
    private static string GenerateSummary(ValidationAccuracyMetrics metrics, string context)
    {
        var status = metrics.MeetsBaselineThreshold ? "PASS" : "FAIL";
        var emoji = metrics.QualityLevel switch
        {
            ValidationAccuracyLevel.Excellent => "🌟",
            ValidationAccuracyLevel.VeryGood => "✅",
            ValidationAccuracyLevel.Good => "✓",
            ValidationAccuracyLevel.Fair => "⚠️",
            ValidationAccuracyLevel.Poor => "❌",
            ValidationAccuracyLevel.Critical => "🚨",
            _ => "?"
        };

        return $"{emoji} {status} | {context} | Accuracy: {metrics.Accuracy:P2} | " +
               $"Precision: {metrics.Precision:P2} | Recall: {metrics.Recall:P2} | " +
               $"F1: {metrics.F1Score:P2} | Level: {metrics.QualityLevel}";
    }
}

/// <summary>
/// Detailed validation accuracy report with recommendations.
/// </summary>
/// <param name="Context">Description of what is being measured</param>
/// <param name="Metrics">Calculated accuracy metrics</param>
/// <param name="MeetsBaseline">Whether accuracy meets the 80% baseline</param>
/// <param name="QualityLevel">Quality classification</param>
/// <param name="Summary">Human-readable summary</param>
/// <param name="Recommendations">Actionable recommendations</param>
/// <param name="Timestamp">Report generation time</param>
internal record ValidationAccuracyReport(
    string Context,
    ValidationAccuracyMetrics Metrics,
    bool MeetsBaseline,
    ValidationAccuracyLevel QualityLevel,
    string Summary,
    IReadOnlyList<string> Recommendations,
    DateTime Timestamp);

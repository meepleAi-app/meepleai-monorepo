using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;

/// <summary>
/// ISSUE-1725: Implementation of IMonthlyOptimizationReportService.
/// Generates comprehensive monthly reports for LLM cost optimization.
/// </summary>
internal class MonthlyOptimizationReportService : IMonthlyOptimizationReportService
{
    private readonly IQueryEfficiencyAnalyzer _efficiencyAnalyzer;
    private readonly IModelRecommendationService _recommendationService;
    private readonly ICacheCorrelationAnalyzer _cacheAnalyzer;
    private readonly ILogger<MonthlyOptimizationReportService> _logger;

    public MonthlyOptimizationReportService(
        IQueryEfficiencyAnalyzer efficiencyAnalyzer,
        IModelRecommendationService recommendationService,
        ICacheCorrelationAnalyzer cacheAnalyzer,
        ILogger<MonthlyOptimizationReportService> logger)
    {
        _efficiencyAnalyzer = efficiencyAnalyzer ?? throw new ArgumentNullException(nameof(efficiencyAnalyzer));
        _recommendationService = recommendationService ?? throw new ArgumentNullException(nameof(recommendationService));
        _cacheAnalyzer = cacheAnalyzer ?? throw new ArgumentNullException(nameof(cacheAnalyzer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MonthlyOptimizationReport> GenerateReportAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        if (month < 1 || month > 12)
        {
            throw new ArgumentException("Month must be between 1 and 12", nameof(month));
        }

        _logger.LogInformation(
            "Generating monthly optimization report for {Year}-{Month:D2}",
            year, month);

        // Calculate date range for the month
        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        // Run all analyses in parallel for efficiency
        var efficiencyTask = _efficiencyAnalyzer.AnalyzeEfficiencyAsync(startDate, endDate, cancellationToken);
        var cacheTask = _cacheAnalyzer.AnalyzeCacheEffectivenessAsync(startDate, endDate, cancellationToken);
        var modelComparisonTask = _recommendationService.CompareModelsAsync(cancellationToken);
        var recommendationTask = _recommendationService.GetRecommendationAsync("qa", prioritizeCost: false, cancellationToken);

        await Task.WhenAll(efficiencyTask, cacheTask, modelComparisonTask, recommendationTask).ConfigureAwait(false);

        var efficiencyAnalysis = await efficiencyTask.ConfigureAwait(false);
        var cacheAnalysis = await cacheTask.ConfigureAwait(false);
        var modelComparisons = await modelComparisonTask.ConfigureAwait(false);
        var recommendation = await recommendationTask.ConfigureAwait(false);

        // Calculate total savings opportunity
        var cacheSavings = cacheAnalysis.EstimatedSavingsUsd;
        var modelSavings = CalculateModelSwitchSavings(efficiencyAnalysis);
        var totalSavings = cacheSavings + modelSavings;

        // Generate executive summary
        var executiveSummary = GenerateExecutiveSummary(
            efficiencyAnalysis,
            cacheAnalysis,
            recommendation,
            totalSavings);

        return new MonthlyOptimizationReport
        {
            Year = year,
            Month = month,
            EfficiencyAnalysis = efficiencyAnalysis,
            CacheAnalysis = cacheAnalysis,
            ModelComparisons = modelComparisons,
            RecommendedModel = recommendation,
            ExecutiveSummary = executiveSummary,
            TotalSavingsOpportunity = totalSavings
        };
    }

    private static decimal CalculateModelSwitchSavings(
        QueryEfficiencyReport efficiency
        )
    {
        // Estimate savings if switching to recommended model
        // Simplified: Assume 20% cost reduction from optimization, adjusted by recommendation confidence
        var baseSavings = efficiency.TotalCost * 0.20m;
        // Recommendation may influence savings estimation in future; for now, return base savings.
        return baseSavings;
    }

    private static List<string> GenerateExecutiveSummary(
        QueryEfficiencyReport efficiency,
        CacheCorrelationReport cache,
        ModelRecommendation recommendation,
        decimal totalSavings)
    {
        var summary = new List<string>
        {
            $"📊 **Monthly LLM Costs**: ${efficiency.TotalCost:F2} ({efficiency.TotalQueries} queries, {efficiency.TotalTokens:N0} tokens)",
            $"💾 **Cache Performance**: {cache.HitRate:P0} hit rate (${cache.EstimatedSavingsUsd:F2} saved)",
            $"🎯 **Recommended Model**: {recommendation.RecommendedModel} ({recommendation.QualityTier} tier)",
            $"💰 **Optimization Opportunity**: ${totalSavings:F2} potential monthly savings"
        };

        // Add top recommendations from each analyzer
        if (efficiency.OptimizationRecommendations.Count > 0)
        {
            summary.Add($"⚡ **Efficiency**: {efficiency.OptimizationRecommendations[0]}");
        }

        if (cache.Recommendations.Count > 0)
        {
            summary.Add($"🔍 **Caching**: {cache.Recommendations[0]}");
        }

        return summary;
    }
}


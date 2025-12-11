using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;

/// <summary>
/// ISSUE-1725: Implementation of IMonthlyOptimizationReportService.
/// Generates comprehensive monthly reports for LLM cost optimization.
/// </summary>
public class MonthlyOptimizationReportService : IMonthlyOptimizationReportService
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
        CancellationToken ct = default)
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
        var efficiencyTask = _efficiencyAnalyzer.AnalyzeEfficiencyAsync(startDate, endDate, ct);
        var cacheTask = _cacheAnalyzer.AnalyzeCacheEffectivenessAsync(startDate, endDate, ct);
        var modelComparisonTask = _recommendationService.CompareModelsAsync(ct);
        var recommendationTask = _recommendationService.GetRecommendationAsync("qa", prioritizeCost: false, ct);

        await Task.WhenAll(efficiencyTask, cacheTask, modelComparisonTask, recommendationTask).ConfigureAwait(false);

        var efficiencyAnalysis = await efficiencyTask.ConfigureAwait(false);
        var cacheAnalysis = await cacheTask.ConfigureAwait(false);
        var modelComparisons = await modelComparisonTask.ConfigureAwait(false);
        var recommendation = await recommendationTask.ConfigureAwait(false);

        // Calculate total savings opportunity
        var cacheSavings = cacheAnalysis.EstimatedSavingsUsd;
        var modelSavings = CalculateModelSwitchSavings(efficiencyAnalysis, recommendation);
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

    private decimal CalculateModelSwitchSavings(
        QueryEfficiencyReport efficiency
        )
    {
        // Estimate savings if switching to recommended model
        // Simplified: Assume 20% cost reduction from optimization
        return efficiency.TotalCost * 0.20m;
    }

    private List<string> GenerateExecutiveSummary(
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
        if (efficiency.OptimizationRecommendations.Any())
        {
            summary.Add($"⚡ **Efficiency**: {efficiency.OptimizationRecommendations.First()}");
        }

        if (cache.Recommendations.Any())
        {
            summary.Add($"🔍 **Caching**: {cache.Recommendations.First()}");
        }

        return summary;
    }
}

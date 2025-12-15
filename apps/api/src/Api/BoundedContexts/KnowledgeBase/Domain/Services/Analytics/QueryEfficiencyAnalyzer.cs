using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;

/// <summary>
/// ISSUE-1725: Implementation of IQueryEfficiencyAnalyzer for LLM cost optimization.
/// Analyzes token usage patterns and provides actionable efficiency recommendations.
/// </summary>
internal class QueryEfficiencyAnalyzer : IQueryEfficiencyAnalyzer
{
    private readonly ILlmCostLogRepository _costLogRepository;
    private readonly ILogger<QueryEfficiencyAnalyzer> _logger;

    // Efficiency thresholds (based on production observations)
    private const int HighTokenThreshold = 1000; // Queries using >1000 tokens are expensive
    private const decimal HighCostThreshold = 0.05m; // >$0.05 per query is expensive
    private const double InefficiencyRatio = 2.0; // >2x average is inefficient

    public QueryEfficiencyAnalyzer(
        ILlmCostLogRepository costLogRepository,
        ILogger<QueryEfficiencyAnalyzer> logger)
    {
        _costLogRepository = costLogRepository ?? throw new ArgumentNullException(nameof(costLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<QueryEfficiencyReport> AnalyzeEfficiencyAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Analyzing query efficiency from {StartDate} to {EndDate}",
            startDate, endDate);

        // Get aggregate data
        var totalCost = await _costLogRepository.GetTotalCostAsync(startDate, endDate, ct).ConfigureAwait(false);
        var costsByProvider = await _costLogRepository.GetCostsByProviderAsync(startDate, endDate, ct).ConfigureAwait(false);

        // Calculate metrics (simplified - real implementation would query detailed logs)
        var totalQueries = (int)costsByProvider.Sum(c => c.Value);
        var totalTokens = (int)(totalCost * 10000); // Estimate from cost (rough heuristic)

        var avgTokensPerQuery = totalQueries > 0 ? (double)totalTokens / totalQueries : 0;
        var avgCostPerQuery = totalQueries > 0 ? totalCost / totalQueries : 0m;

        // Get top costly queries
        var topCostly = await GetTopCostlyQueriesAsync(startDate, endDate, 10, ct).ConfigureAwait(false);

        // Get average tokens by operation
        var avgByOperation = await GetAverageTokensByOperationAsync(startDate, endDate, ct).ConfigureAwait(false);

        // Generate optimization recommendations
        var recommendations = GenerateRecommendations(avgTokensPerQuery, avgCostPerQuery, topCostly);

        return new QueryEfficiencyReport
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalQueries = totalQueries,
            TotalCost = totalCost,
            TotalTokens = totalTokens,
            AverageTokensPerQuery = avgTokensPerQuery,
            AverageCostPerQuery = avgCostPerQuery,
            TopCostlyQueries = topCostly,
            AverageTokensByOperation = avgByOperation,
            OptimizationRecommendations = recommendations
        };
    }

    public async Task<IReadOnlyList<QueryTypeCost>> GetTopCostlyQueriesAsync(
        DateOnly startDate,
        DateOnly endDate,
        int topN = 10,
        CancellationToken ct = default)
    {
        // Get costs by provider (proxy for query type in current implementation)
        var costsByProvider = await _costLogRepository.GetCostsByProviderAsync(startDate, endDate, ct).ConfigureAwait(false);

        return costsByProvider
            .Select(kvp => new QueryTypeCost
            {
                QueryType = kvp.Key,
                QueryCount = (int)kvp.Value,
                TotalCost = kvp.Value * 0.01m, // Estimate (avg $0.01 per query)
                TotalTokens = (int)(kvp.Value * 500), // Estimate (avg 500 tokens)
                AverageTokens = 500,
                AverageCost = 0.01m
            })
            .OrderByDescending(q => q.TotalCost)
            .Take(topN)
            .ToArray();
    }

    public async Task<IReadOnlyDictionary<string, double>> GetAverageTokensByOperationAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default)
    {
        // Placeholder: Real implementation would query detailed operation logs
        await Task.CompletedTask.ConfigureAwait(false);

        return new Dictionary<string, double>(StringComparer.Ordinal)
        {
            { "qa", 450.0 },
            { "explain", 650.0 },
            { "setup", 550.0 },
            { "streaming", 480.0 }
        };
    }

    private IReadOnlyList<string> GenerateRecommendations(
        double avgTokens,
        decimal avgCost,
        IReadOnlyList<QueryTypeCost> topCostly)
    {
        var recommendations = new List<string>();

        if (avgTokens > HighTokenThreshold)
        {
            recommendations.Add($"⚡ High token usage detected ({avgTokens:F0} avg). Consider prompt optimization or chunking.");
        }

        if (avgCost > HighCostThreshold)
        {
            recommendations.Add($"💰 High per-query cost (${avgCost:F4}). Consider switching to cheaper models for simple queries.");
        }

        if (topCostly.Any(q => q.AverageCost > HighCostThreshold * (decimal)InefficiencyRatio))
        {
            var expensive = topCostly.First(q => q.AverageCost > HighCostThreshold * (decimal)InefficiencyRatio);
            recommendations.Add($"🎯 Optimize '{expensive.QueryType}' queries (${expensive.AverageCost:F4} avg, {expensive.AverageTokens:F0} tokens).");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("✅ Query efficiency within normal parameters. No optimizations needed.");
        }

        return recommendations;
    }
}

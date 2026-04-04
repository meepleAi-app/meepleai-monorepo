using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Configuration;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries;

/// <summary>
/// Handler for estimating agent costs based on strategy, model, and usage parameters.
/// Issue #3725: Agent Cost Calculator (Epic #3688)
/// </summary>
internal sealed class EstimateAgentCostQueryHandler
    : IQueryHandler<EstimateAgentCostQuery, AgentCostEstimationResult>
{
    private const decimal InputOutputRatio = 0.7m;
    private const int DaysPerMonth = 30;

    private readonly ILlmCostCalculator _costCalculator;
    private readonly ILogger<EstimateAgentCostQueryHandler> _logger;

    public EstimateAgentCostQueryHandler(
        ILlmCostCalculator costCalculator,
        ILogger<EstimateAgentCostQueryHandler> logger)
    {
        _costCalculator = costCalculator ?? throw new ArgumentNullException(nameof(costCalculator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<AgentCostEstimationResult> Handle(
        EstimateAgentCostQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var warnings = new List<string>();

        // Parse strategy
        if (!Enum.TryParse<RagStrategy>(query.Strategy, ignoreCase: true, out var strategy))
        {
            warnings.Add($"Unknown strategy '{query.Strategy}', using generic estimate.");
            return Task.FromResult(BuildFallbackResult(query, warnings));
        }

        // Get strategy mapping for provider and cost info
        if (!DefaultStrategyModelMappings.HasMapping(strategy))
        {
            warnings.Add($"No mapping for strategy '{query.Strategy}'.");
            return Task.FromResult(BuildFallbackResult(query, warnings));
        }

        var strategyMapping = DefaultStrategyModelMappings.GetMapping(strategy);
        var modelId = string.IsNullOrWhiteSpace(query.ModelId)
            ? strategyMapping.PrimaryModel
            : query.ModelId;

        // Try to get model-specific pricing from LlmCostCalculator
        var modelPricing = _costCalculator.GetModelPricing(modelId);

        decimal inputCostPer1M;
        decimal outputCostPer1M;
        string provider;

        if (modelPricing != null)
        {
            inputCostPer1M = modelPricing.InputCostPer1M;
            outputCostPer1M = modelPricing.OutputCostPer1M;
            provider = modelPricing.Provider;
        }
        else if (strategyMapping.EstimatedCostPer1KTokens.HasValue)
        {
            // Convert strategy cost per 1K tokens to approximate per 1M input/output split
            var totalCostPer1M = strategyMapping.EstimatedCostPer1KTokens.Value * 1000m;
            inputCostPer1M = Math.Round(totalCostPer1M * InputOutputRatio, 6);
            outputCostPer1M = Math.Round(totalCostPer1M * (1m - InputOutputRatio), 6);
            provider = strategyMapping.Provider;
            warnings.Add($"Model '{modelId}' not found in pricing database. Using strategy-level estimate.");
        }
        else
        {
            // Custom strategy with no pricing data
            warnings.Add($"Strategy '{query.Strategy}' has variable cost. No estimate available for model '{modelId}'.");
            return Task.FromResult(BuildFallbackResult(query, warnings));
        }

        // Calculate costs
        var avgTokens = query.AvgTokensPerRequest;
        var inputTokens = (int)(avgTokens * InputOutputRatio);
        var outputTokens = avgTokens - inputTokens;

        var inputCost = (inputTokens / 1_000_000m) * inputCostPer1M;
        var outputCost = (outputTokens / 1_000_000m) * outputCostPer1M;
        var costPerRequest = Math.Round(inputCost + outputCost, 8);

        var totalDailyRequests = (long)query.MessagesPerDay * query.ActiveUsers;
        var dailyProjection = Math.Round(costPerRequest * totalDailyRequests, 4);
        var monthlyProjection = Math.Round(dailyProjection * DaysPerMonth, 4);

        // Generate warnings
        if (modelPricing is { IsFree: true })
            warnings.Add("This model is free tier. Costs may change if free tier limits are exceeded.");

        if (monthlyProjection > 10_000m)
            warnings.Add("Monthly projection exceeds $10,000. Consider using a more cost-efficient strategy or model.");
        else if (monthlyProjection > 1_000m)
            warnings.Add("Monthly projection exceeds $1,000. Review usage parameters for optimization opportunities.");

        if (totalDailyRequests > 100_000)
            warnings.Add("High request volume. Consider rate limiting or caching to reduce costs.");

        if (strategy == RagStrategy.MultiAgent)
            warnings.Add("MultiAgent strategy uses multiple models per request. Actual costs may vary based on agent routing.");

        if (strategy == RagStrategy.Custom)
            warnings.Add("Custom strategy has variable costs depending on configuration.");

        _logger.LogDebug(
            "Cost estimate for {Strategy}/{Model}: ${CostPerRequest:F8}/req, ${Monthly:F2}/mo ({Requests}/day)",
            query.Strategy, modelId, costPerRequest, monthlyProjection, totalDailyRequests);

        var result = new AgentCostEstimationResult(
            Strategy: query.Strategy,
            ModelId: modelId,
            Provider: provider,
            InputCostPer1MTokens: inputCostPer1M,
            OutputCostPer1MTokens: outputCostPer1M,
            CostPerRequest: costPerRequest,
            DailyProjection: dailyProjection,
            MonthlyProjection: monthlyProjection,
            TotalDailyRequests: totalDailyRequests,
            AvgTokensPerRequest: avgTokens,
            Warnings: warnings);

        return Task.FromResult(result);
    }

    private static AgentCostEstimationResult BuildFallbackResult(
        EstimateAgentCostQuery query,
        List<string> warnings)
    {
        return new AgentCostEstimationResult(
            Strategy: query.Strategy,
            ModelId: query.ModelId,
            Provider: "Unknown",
            InputCostPer1MTokens: 0m,
            OutputCostPer1MTokens: 0m,
            CostPerRequest: 0m,
            DailyProjection: 0m,
            MonthlyProjection: 0m,
            TotalDailyRequests: (long)query.MessagesPerDay * query.ActiveUsers,
            AvgTokensPerRequest: query.AvgTokensPerRequest,
            Warnings: warnings);
    }
}

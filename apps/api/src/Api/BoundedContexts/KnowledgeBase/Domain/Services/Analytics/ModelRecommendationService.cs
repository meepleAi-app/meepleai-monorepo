using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;

/// <summary>
/// ISSUE-1725: Implementation of IModelRecommendationService for cost/quality optimization.
/// Provides data-driven model selection recommendations.
/// </summary>
public class ModelRecommendationService : IModelRecommendationService
{
    private readonly ILlmCostCalculator _costCalculator;
    private readonly ILogger<ModelRecommendationService> _logger;

    public ModelRecommendationService(
        ILlmCostCalculator costCalculator,
        ILogger<ModelRecommendationService> logger)
    {
        _costCalculator = costCalculator ?? throw new ArgumentNullException(nameof(costCalculator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<ModelRecommendation> GetRecommendationAsync(
        string useCase,
        bool prioritizeCost = false,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Generating model recommendation for use case: {UseCase}, prioritize cost: {PrioritizeCost}",
            useCase, prioritizeCost);

        // Model selection matrix based on use case
        var recommendation = (useCase.ToLowerInvariant(), prioritizeCost) switch
        {
            // Cost-optimized recommendations
            ("qa", true) => CreateRecommendation(
                "meta-llama/llama-3.3-70b-instruct:free",
                "OpenRouter",
                0m,
                "Free",
                "Zero cost with acceptable quality for simple Q&A",
                ["openai/gpt-4o-mini", "deepseek/deepseek-chat"]),

            ("explain", true) => CreateRecommendation(
                "openai/gpt-4o-mini",
                "OpenRouter",
                0.15m,
                "Budget",
                "Best cost/quality for explanations ($0.15/$0.60 per 1M tokens)",
                ["anthropic/claude-3.5-haiku", "deepseek/deepseek-chat"]),

            // Quality-optimized recommendations
            ("qa", false) => CreateRecommendation(
                "openai/gpt-4o-mini",
                "OpenRouter",
                0.15m,
                "Standard",
                "Balanced cost/quality for Q&A with good accuracy",
                ["anthropic/claude-3.5-haiku", "openai/gpt-4o"]),

            ("explain", false) => CreateRecommendation(
                "anthropic/claude-3.5-sonnet",
                "OpenRouter",
                3.00m,
                "Premium",
                "Superior quality for complex explanations",
                ["openai/gpt-4o", "anthropic/claude-3-opus"]),

            ("setup", _) => CreateRecommendation(
                "openai/gpt-4o-mini",
                "OpenRouter",
                0.15m,
                "Standard",
                "Good structured output for setup guides",
                ["anthropic/claude-3.5-haiku"]),

            _ => CreateRecommendation(
                "openai/gpt-4o-mini",
                "OpenRouter",
                0.15m,
                "Standard",
                "Default recommendation for unknown use cases",
                ["meta-llama/llama-3.3-70b-instruct:free"])
        };

        return Task.FromResult(recommendation);
    }

    public async Task<List<ModelComparison>> CompareModelsAsync(CancellationToken ct = default)
    {
        await Task.CompletedTask.ConfigureAwait(false); // Satisfy async

        var models = new[]
        {
            "openai/gpt-4o", "openai/gpt-4o-mini",
            "anthropic/claude-3-opus", "anthropic/claude-3.5-sonnet", "anthropic/claude-3.5-haiku",
            "deepseek/deepseek-chat",
            "meta-llama/llama-3.3-70b-instruct:free"
        };

        var comparisons = new List<ModelComparison>();

        foreach (var model in models)
        {
            var pricing = _costCalculator.GetModelPricing(model);
            if (pricing == null) continue;

            var qualityTier = DetermineQualityTier(pricing.InputCostPer1M);
            var costQualityRatio = CalculateCostQualityRatio(pricing.InputCostPer1M, qualityTier);

            comparisons.Add(new ModelComparison
            {
                ModelId = model,
                Provider = pricing.Provider,
                InputCostPer1M = pricing.InputCostPer1M,
                OutputCostPer1M = pricing.OutputCostPer1M,
                QualityTier = qualityTier,
                CostQualityRatio = costQualityRatio
            });
        }

        return comparisons.OrderBy(c => c.CostQualityRatio).ToList();
    }

    private ModelRecommendation CreateRecommendation(
        string model,
        string provider,
        decimal costPer1M,
        string qualityTier,
        string rationale,
        string[] alternatives)
    {
        return new ModelRecommendation
        {
            RecommendedModel = model,
            Provider = provider,
            EstimatedCostPer1MTokens = costPer1M,
            QualityTier = qualityTier,
            Rationale = rationale,
            AlternativeModels = alternatives.ToList()
        };
    }

    private static string DetermineQualityTier(decimal inputCostPer1M) => inputCostPer1M switch
    {
        0m => "Free",
        < 1.0m => "Budget",
        < 3.0m => "Standard",
        < 10.0m => "Premium",
        _ => "Ultra"
    };

    private static double CalculateCostQualityRatio(decimal cost, string quality)
    {
        var qualityScore = quality switch
        {
            "Free" => 0.5,
            "Budget" => 0.7,
            "Standard" => 0.85,
            "Premium" => 0.95,
            "Ultra" => 1.0,
            _ => 0.5
        };

        return cost == 0 ? 0.0 : (double)cost / qualityScore;
    }
}

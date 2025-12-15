

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;

/// <summary>
/// ISSUE-1725: Provides LLM model recommendations based on cost/quality trade-offs.
/// Analyzes historical performance to suggest optimal model selection.
/// </summary>
internal interface IModelRecommendationService
{
    /// <summary>
    /// Get model recommendation for a use case based on cost and quality requirements
    /// </summary>
    /// <param name="useCase">Use case type (qa, explain, setup)</param>
    /// <param name="prioritizeCost">True to prioritize cost over quality</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Recommended model with rationale</returns>
    Task<ModelRecommendation> GetRecommendationAsync(
        string useCase,
        bool prioritizeCost = false,
        CancellationToken ct = default);

    /// <summary>
    /// Compare models by cost and quality metrics
    /// </summary>
    Task<List<ModelComparison>> CompareModelsAsync(CancellationToken ct = default);
}

/// <summary>
/// Model recommendation with rationale
/// </summary>
internal record ModelRecommendation
{
    public required string RecommendedModel { get; init; }
    public required string Provider { get; init; }
    public required decimal EstimatedCostPer1MTokens { get; init; }
    public required string QualityTier { get; init; }
    public required string Rationale { get; init; }
    public required List<string> AlternativeModels { get; init; }
}

/// <summary>
/// Model comparison metrics
/// </summary>
internal record ModelComparison
{
    public required string ModelId { get; init; }
    public required string Provider { get; init; }
    public required decimal InputCostPer1M { get; init; }
    public required decimal OutputCostPer1M { get; init; }
    public required string QualityTier { get; init; }
    public required double CostQualityRatio { get; init; }
}

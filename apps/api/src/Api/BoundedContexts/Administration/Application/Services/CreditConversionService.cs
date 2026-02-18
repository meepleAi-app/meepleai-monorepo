using Api.BoundedContexts.KnowledgeBase.Domain.Services;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Stateless service for converting between USD and credits.
/// Conversion: 1 credit = $0.00001 USD (1:100,000 ratio)
/// </summary>
public interface ICreditConversionService
{
    /// <summary>
    /// Convert USD to credits (1:100,000 ratio)
    /// </summary>
    /// <param name="usd">USD amount (e.g., $0.00001)</param>
    /// <returns>Credits (e.g., 1)</returns>
    decimal UsdToCredits(decimal usd);

    /// <summary>
    /// Convert credits to USD (100,000:1 ratio)
    /// </summary>
    /// <param name="credits">Credits (e.g., 1)</param>
    /// <returns>USD amount (e.g., $0.00001)</returns>
    decimal CreditsToUsd(decimal credits);

    /// <summary>
    /// Estimate credits needed for a query based on expected token count
    /// </summary>
    /// <param name="estimatedTokens">Estimated input + output tokens</param>
    /// <param name="modelId">LLM model identifier</param>
    /// <returns>Estimated credits needed</returns>
    decimal EstimateQueryCredits(int estimatedTokens, string modelId);
}

/// <summary>
/// Implementation of credit conversion service with cost estimation
/// </summary>
internal sealed class CreditConversionService : ICreditConversionService
{
    private const decimal CREDITS_PER_DOLLAR = 100_000m;

    private readonly ILlmCostCalculator _costCalculator;
    private readonly ILogger<CreditConversionService> _logger;

    public CreditConversionService(
        ILlmCostCalculator costCalculator,
        ILogger<CreditConversionService> logger)
    {
        _costCalculator = costCalculator ?? throw new ArgumentNullException(nameof(costCalculator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public decimal UsdToCredits(decimal usd)
    {
        if (usd < 0)
            throw new ArgumentException("USD amount cannot be negative", nameof(usd));

        // Round up to protect user (avoid under-deduction)
        return Math.Ceiling(usd * CREDITS_PER_DOLLAR);
    }

    /// <inheritdoc />
    public decimal CreditsToUsd(decimal credits)
    {
        if (credits < 0)
            throw new ArgumentException("Credits cannot be negative", nameof(credits));

        return credits / CREDITS_PER_DOLLAR;
    }

    /// <inheritdoc />
    public decimal EstimateQueryCredits(int estimatedTokens, string modelId)
    {
        if (estimatedTokens < 0)
            throw new ArgumentException("Estimated tokens cannot be negative", nameof(estimatedTokens));

        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("Model ID cannot be empty", nameof(modelId));

        // Get model pricing
        var pricing = _costCalculator.GetModelPricing(modelId);
        if (pricing == null)
        {
            _logger.LogWarning("No pricing found for model {ModelId}, treating as free", modelId);
            return 0m;
        }

        // Estimate cost: assume 70% input, 30% output for balanced estimate
        var estimatedInputTokens = (int)(estimatedTokens * 0.7);
        var estimatedOutputTokens = (int)(estimatedTokens * 0.3);

        var estimatedCost =
            (estimatedInputTokens / 1_000_000m) * pricing.InputCostPer1M +
            (estimatedOutputTokens / 1_000_000m) * pricing.OutputCostPer1M;

        var credits = UsdToCredits(estimatedCost);

        _logger.LogDebug(
            "Estimated {Credits} credits for {Tokens} tokens on {Model} (${Cost:F6})",
            credits, estimatedTokens, modelId, estimatedCost);

        return credits;
    }
}

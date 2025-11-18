using Api.BoundedContexts.KnowledgeBase.Domain.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for calculating LLM request costs
/// ISSUE-960: BGAI-018 - Cost tracking implementation
/// </summary>
public interface ILlmCostCalculator
{
    /// <summary>
    /// Calculate cost for an LLM request given token usage
    /// </summary>
    /// <param name="modelId">Model identifier (e.g., "openai/gpt-4o-mini")</param>
    /// <param name="provider">Provider name (e.g., "OpenRouter", "Ollama")</param>
    /// <param name="promptTokens">Number of input/prompt tokens</param>
    /// <param name="completionTokens">Number of output/completion tokens</param>
    /// <returns>Cost calculation with breakdown</returns>
    LlmCostCalculation CalculateCost(
        string modelId,
        string provider,
        int promptTokens,
        int completionTokens);

    /// <summary>
    /// Get pricing information for a specific model
    /// </summary>
    /// <param name="modelId">Model identifier</param>
    /// <returns>Pricing information, or null if model not found</returns>
    LlmModelPricing? GetModelPricing(string modelId);
}

using Api.BoundedContexts.KnowledgeBase.Domain.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for calculating LLM request costs based on token usage
/// ISSUE-960: BGAI-018 - Financial cost tracking implementation
/// </summary>
/// <remarks>
/// Pricing data sourced from OpenRouter (https://openrouter.ai/models) as of 2025-11-12.
/// Free tier models (meta-llama/*:free) and local Ollama models have $0 cost.
///
/// Pricing Strategy:
/// - Anonymous/User: meta-llama/llama-3.3-70b-instruct:free ($0)
/// - Editor/Admin Ollama: llama3:8b ($0 - self-hosted)
/// - Paid fallback: openai/gpt-4o-mini ($0.15/$0.60 per 1M tokens)
/// - Admin premium: anthropic/claude-3.5-haiku ($0.80/$4.00 per 1M tokens)
/// </remarks>
internal class LlmCostCalculator : ILlmCostCalculator
{
    // Pricing database (per 1M tokens in USD)
    // Source: OpenRouter pricing + Anthropic direct pricing (Q1 2025)
    private static readonly Dictionary<string, LlmModelPricing> ModelPricing = new(StringComparer.Ordinal)
    {
        // OpenRouter Models - OpenAI
        ["openai/gpt-4o-mini"] = new()
        {
            ModelId = "openai/gpt-4o-mini",
            Provider = "OpenRouter",
            InputCostPer1M = 0.15m,  // $0.15 per 1M input tokens
            OutputCostPer1M = 0.60m  // $0.60 per 1M output tokens
        },
        ["openai/gpt-4o"] = new()
        {
            ModelId = "openai/gpt-4o",
            Provider = "OpenRouter",
            InputCostPer1M = 2.50m,
            OutputCostPer1M = 10.00m
        },

        // OpenRouter Models - Anthropic
        ["anthropic/claude-3.5-haiku"] = new()
        {
            ModelId = "anthropic/claude-3.5-haiku",
            Provider = "OpenRouter",
            InputCostPer1M = 0.80m,  // $0.80 per 1M input tokens
            OutputCostPer1M = 4.00m  // $4.00 per 1M output tokens
        },
        ["anthropic/claude-3.5-sonnet"] = new()
        {
            ModelId = "anthropic/claude-3.5-sonnet",
            Provider = "OpenRouter",
            InputCostPer1M = 3.00m,
            OutputCostPer1M = 15.00m
        },
        ["anthropic/claude-3-opus"] = new()
        {
            ModelId = "anthropic/claude-3-opus",
            Provider = "OpenRouter",
            InputCostPer1M = 15.00m,
            OutputCostPer1M = 75.00m
        },

        // OpenRouter Models - Meta (Free Tier)
        ["meta-llama/llama-3.3-70b-instruct:free"] = new()
        {
            ModelId = "meta-llama/llama-3.3-70b-instruct:free",
            Provider = "OpenRouter",
            InputCostPer1M = 0m,  // Free tier
            OutputCostPer1M = 0m
        },
        ["meta-llama/llama-3.1-70b-instruct:free"] = new()
        {
            ModelId = "meta-llama/llama-3.1-70b-instruct:free",
            Provider = "OpenRouter",
            InputCostPer1M = 0m,
            OutputCostPer1M = 0m
        },

        // OpenRouter Models - DeepSeek
        ["deepseek/deepseek-chat"] = new()
        {
            ModelId = "deepseek/deepseek-chat",
            Provider = "OpenRouter",
            InputCostPer1M = 0.27m,
            OutputCostPer1M = 1.10m
        },

        // Local Ollama Models (self-hosted, zero cost)
        ["llama3:8b"] = new()
        {
            ModelId = "llama3:8b",
            Provider = "Ollama",
            InputCostPer1M = 0m,  // Self-hosted
            OutputCostPer1M = 0m
        },
        ["llama3:70b"] = new()
        {
            ModelId = "llama3:70b",
            Provider = "Ollama",
            InputCostPer1M = 0m,
            OutputCostPer1M = 0m
        },
        ["mistral"] = new()
        {
            ModelId = "mistral",
            Provider = "Ollama",
            InputCostPer1M = 0m,
            OutputCostPer1M = 0m
        }
    };

    private readonly ILogger<LlmCostCalculator> _logger;

    public LlmCostCalculator(ILogger<LlmCostCalculator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public LlmCostCalculation CalculateCost(
        string modelId,
        string provider,
        int promptTokens,
        int completionTokens)
    {
        if (promptTokens < 0 || completionTokens < 0)
        {
            _logger.LogWarning("Invalid token counts: prompt={Prompt}, completion={Completion}",
                promptTokens, completionTokens);
            return LlmCostCalculation.Empty;
        }

        // Get pricing for model
        var pricing = GetModelPricing(modelId);
        if (pricing == null)
        {
            _logger.LogWarning("No pricing found for model {ModelId}, treating as free", modelId);
            pricing = LlmModelPricing.Free with { ModelId = modelId, Provider = provider };
        }

        // Calculate costs (tokens / 1M * cost_per_1M)
        var inputCost = (promptTokens / 1_000_000m) * pricing.InputCostPer1M;
        var outputCost = (completionTokens / 1_000_000m) * pricing.OutputCostPer1M;

        var calculation = new LlmCostCalculation
        {
            ModelId = modelId,
            Provider = provider,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            InputCost = Math.Round(inputCost, 6),  // Round to 6 decimal places (micro-dollars)
            OutputCost = Math.Round(outputCost, 6)
        };

        _logger.LogDebug(
            "Cost calculated for {Model}: {Tokens} tokens = ${Cost:F6} (input: ${InputCost:F6}, output: ${OutputCost:F6})",
            modelId, calculation.TotalTokens, calculation.TotalCost, calculation.InputCost, calculation.OutputCost);

        return calculation;
    }

    /// <inheritdoc/>
    public LlmModelPricing? GetModelPricing(string modelId)
    {
        return ModelPricing.TryGetValue(modelId, out var pricing) ? pricing : null;
    }

    /// <summary>
    /// Get all available model pricing information
    /// </summary>
    public IReadOnlyDictionary<string, LlmModelPricing> GetAllPricing() => ModelPricing;
}



#pragma warning disable MA0048 // File name must match type name - Contains related domain models
namespace Api.BoundedContexts.KnowledgeBase.Domain.Models;

/// <summary>
/// Domain model for LLM model pricing information
/// ISSUE-960: BGAI-018 - Cost tracking for OpenRouter models
/// </summary>
/// <remarks>
/// Pricing is per 1M tokens (standard LLM pricing unit).
/// Free tier models (meta-llama/*:free) have $0 cost.
/// Local Ollama models have $0 cost (self-hosted).
/// </remarks>
internal record LlmModelPricing
{
    /// <summary>
    /// Model identifier (e.g., "openai/gpt-4o-mini", "anthropic/claude-3.5-haiku")
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Provider name (e.g., "OpenRouter", "Ollama")
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Cost per 1M input/prompt tokens in USD
    /// </summary>
    public required decimal InputCostPer1M { get; init; }

    /// <summary>
    /// Cost per 1M output/completion tokens in USD
    /// </summary>
    public required decimal OutputCostPer1M { get; init; }

    /// <summary>
    /// Whether this is a free tier model (no cost)
    /// </summary>
    public bool IsFree => InputCostPer1M == 0 && OutputCostPer1M == 0;

    /// <summary>
    /// Default free model (zero cost)
    /// </summary>
    public static readonly LlmModelPricing Free = new()
    {
        ModelId = "free",
        Provider = "Unknown",
        InputCostPer1M = 0,
        OutputCostPer1M = 0
    };
}

/// <summary>
/// Cost calculation result for an LLM request
/// </summary>
internal record LlmCostCalculation
{
    /// <summary>
    /// Model used for the request
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Provider used (OpenRouter, Ollama)
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Number of input/prompt tokens
    /// </summary>
    public required int PromptTokens { get; init; }

    /// <summary>
    /// Number of output/completion tokens
    /// </summary>
    public required int CompletionTokens { get; init; }

    /// <summary>
    /// Total tokens (prompt + completion)
    /// </summary>
    public int TotalTokens => PromptTokens + CompletionTokens;

    /// <summary>
    /// Cost for input tokens in USD
    /// </summary>
    public required decimal InputCost { get; init; }

    /// <summary>
    /// Cost for output tokens in USD
    /// </summary>
    public required decimal OutputCost { get; init; }

    /// <summary>
    /// Total cost in USD (input + output)
    /// </summary>
    public decimal TotalCost => InputCost + OutputCost;

    /// <summary>
    /// Whether this was a free request (zero cost)
    /// </summary>
    public bool IsFree => TotalCost == 0;

    /// <summary>
    /// Empty cost calculation (zero cost, zero tokens)
    /// </summary>
    public static readonly LlmCostCalculation Empty = new()
    {
        ModelId = "unknown",
        Provider = "Unknown",
        PromptTokens = 0,
        CompletionTokens = 0,
        InputCost = 0,
        OutputCost = 0
    };
}

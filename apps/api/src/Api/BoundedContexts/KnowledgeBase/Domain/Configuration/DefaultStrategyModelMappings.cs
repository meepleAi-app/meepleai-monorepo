using Api.BoundedContexts.KnowledgeBase.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Configuration;

/// <summary>
/// Default strategy-to-model mappings configuration.
/// Issue #3437: Centralized configuration for strategy routing.
/// </summary>
/// <remarks>
/// Default mappings per tier-strategy-model architecture:
/// - FAST: Free tier, quick lookups
/// - BALANCED: Cost-efficient standard queries
/// - PRECISE: High-quality reasoning
/// - EXPERT: Advanced research capabilities
/// - CONSENSUS: Multi-model validation
/// - SENTENCE_WINDOW: Overlapping document windows for precise citations
/// - CUSTOM: Admin-configurable workflows
/// </remarks>
public static class DefaultStrategyModelMappings
{
    /// <summary>
    /// Default mappings for all RAG strategies.
    /// </summary>
    public static IReadOnlyDictionary<RagStrategy, StrategyMappingConfig> Mappings { get; } = new Dictionary<RagStrategy, StrategyMappingConfig>
    {
        [RagStrategy.Fast] = new StrategyMappingConfig(
            Provider: "OpenRouter",
            PrimaryModel: "meta-llama/llama-3.3-70b-instruct:free",
            FallbackModels: Array.Empty<string>(),
            IsCustomizable: false,
            EstimatedCostPer1KTokens: 0.0m),

        [RagStrategy.Balanced] = new StrategyMappingConfig(
            Provider: "DeepSeek",
            PrimaryModel: "deepseek-chat",
            FallbackModels: new[] { "meta-llama/llama-3.3-70b-instruct:free" },
            IsCustomizable: false,
            EstimatedCostPer1KTokens: 0.01m),

        [RagStrategy.Precise] = new StrategyMappingConfig(
            Provider: "Anthropic",
            PrimaryModel: "anthropic/claude-sonnet-4.5",
            FallbackModels: new[] { "openai/gpt-4o-mini" },
            IsCustomizable: false,
            EstimatedCostPer1KTokens: 0.13m),

        [RagStrategy.Expert] = new StrategyMappingConfig(
            Provider: "Anthropic",
            PrimaryModel: "anthropic/claude-sonnet-4.5",
            FallbackModels: new[] { "openai/gpt-4o" },
            IsCustomizable: false,
            EstimatedCostPer1KTokens: 0.10m),

        [RagStrategy.Consensus] = new StrategyMappingConfig(
            Provider: "Mixed",
            PrimaryModel: "anthropic/claude-sonnet-4.5",
            FallbackModels: new[] { "openai/gpt-4o", "google/gemini-pro" },
            IsCustomizable: false,
            EstimatedCostPer1KTokens: 0.09m),

        [RagStrategy.SentenceWindow] = new StrategyMappingConfig(
            Provider: "Anthropic",
            PrimaryModel: "anthropic/claude-sonnet-4.5",
            FallbackModels: new[] { "openai/gpt-4o-mini" },
            IsCustomizable: false,
            EstimatedCostPer1KTokens: 0.08m), // ~3,250 tokens avg, +7% accuracy

        [RagStrategy.Custom] = new StrategyMappingConfig(
            Provider: "Anthropic",
            PrimaryModel: "anthropic/claude-haiku-4.5",
            FallbackModels: Array.Empty<string>(),
            IsCustomizable: true,
            EstimatedCostPer1KTokens: null) // Variable cost
    };

    /// <summary>
    /// Gets the default mapping for a strategy.
    /// </summary>
    /// <param name="strategy">The RAG strategy.</param>
    /// <returns>The default mapping configuration.</returns>
    /// <exception cref="ArgumentException">Thrown if strategy not found.</exception>
    public static StrategyMappingConfig GetMapping(RagStrategy strategy)
    {
        if (Mappings.TryGetValue(strategy, out var mapping))
            return mapping;

        throw new ArgumentException(
            $"No default mapping found for strategy: {strategy}",
            nameof(strategy));
    }

    /// <summary>
    /// Checks if a strategy has a default mapping.
    /// </summary>
    public static bool HasMapping(RagStrategy strategy) => Mappings.ContainsKey(strategy);
}

/// <summary>
/// Configuration record for a strategy-model mapping.
/// </summary>
/// <param name="Provider">LLM provider name (OpenRouter, Anthropic, DeepSeek, Mixed).</param>
/// <param name="PrimaryModel">Primary model ID for this strategy.</param>
/// <param name="FallbackModels">Fallback model IDs if primary fails.</param>
/// <param name="IsCustomizable">Whether users can customize model selection.</param>
/// <param name="EstimatedCostPer1KTokens">Estimated cost per 1K tokens (null for variable).</param>
public record StrategyMappingConfig(
    string Provider,
    string PrimaryModel,
    string[] FallbackModels,
    bool IsCustomizable,
    decimal? EstimatedCostPer1KTokens);

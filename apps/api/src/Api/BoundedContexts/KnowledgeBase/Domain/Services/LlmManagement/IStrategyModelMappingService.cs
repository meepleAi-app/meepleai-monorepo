using Api.BoundedContexts.KnowledgeBase.Domain.Configuration;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Service for retrieving strategy-to-model mappings.
/// Issue #3435: Part of tier-strategy-model architecture.
/// </summary>
/// <remarks>
/// Strategy determines which LLM model is used, independent of user tier.
/// User tier only validates ACCESS to a strategy (via ITierStrategyAccessService).
/// </remarks>
public interface IStrategyModelMappingService
{
    /// <summary>
    /// Gets the model mapping for a specific strategy.
    /// Results are cached with 5-minute TTL.
    /// </summary>
    /// <param name="strategy">The RAG strategy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The model mapping if found, null otherwise.</returns>
    Task<StrategyModelMapping?> GetModelMappingAsync(RagStrategy strategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the primary model for a strategy, with fallback to defaults.
    /// </summary>
    /// <param name="strategy">The RAG strategy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider name and model ID.</returns>
    Task<(string Provider, string ModelId)> GetModelForStrategyAsync(RagStrategy strategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets fallback models for a strategy if primary fails.
    /// </summary>
    /// <param name="strategy">The RAG strategy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of fallback model IDs.</returns>
    Task<IReadOnlyList<string>> GetFallbackModelsAsync(RagStrategy strategy, CancellationToken cancellationToken = default);
}

/// <summary>
/// Value object representing a strategy-model mapping.
/// </summary>
/// <param name="Strategy">The RAG strategy.</param>
/// <param name="Provider">The LLM provider name (OpenRouter, Ollama).</param>
/// <param name="PrimaryModel">The primary model ID.</param>
/// <param name="FallbackModels">Fallback model IDs if primary fails.</param>
/// <param name="IsCustomizable">Whether users can customize model selection.</param>
public record StrategyModelMapping(
    RagStrategy Strategy,
    string Provider,
    string PrimaryModel,
    IReadOnlyList<string> FallbackModels,
    bool IsCustomizable)
{
    /// <summary>
    /// Creates a default mapping for a strategy when database has no entry.
    /// Issue #3437: Delegates to DefaultStrategyModelMappings configuration.
    /// </summary>
    public static StrategyModelMapping Default(RagStrategy strategy)
    {
        // Use centralized configuration from Issue #3437
        var config = DefaultStrategyModelMappings.GetMapping(strategy);

        return new StrategyModelMapping(
            strategy,
            config.Provider,
            config.PrimaryModel,
            config.FallbackModels,
            config.IsCustomizable);
    }
}

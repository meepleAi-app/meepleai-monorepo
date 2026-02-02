using Api.BoundedContexts.KnowledgeBase.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository for strategy-to-model mapping records.
/// Issue #3435: Part of tier-strategy-model architecture.
/// </summary>
public interface IStrategyModelMappingRepository
{
    /// <summary>
    /// Gets the model mapping for a specific strategy.
    /// </summary>
    /// <param name="strategy">The RAG strategy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The model mapping if found, null otherwise.</returns>
    Task<StrategyModelMappingEntry?> GetByStrategyAsync(RagStrategy strategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all strategy-model mappings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all strategy-model mappings.</returns>
    Task<IReadOnlyList<StrategyModelMappingEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a mapping exists for the specified strategy.
    /// </summary>
    /// <param name="strategy">The RAG strategy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if mapping exists.</returns>
    Task<bool> HasMappingAsync(RagStrategy strategy, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read model for strategy-model mapping entry.
/// </summary>
/// <param name="Strategy">The RAG strategy name.</param>
/// <param name="PrimaryModel">The primary LLM model ID.</param>
/// <param name="FallbackModels">Fallback model IDs if primary fails.</param>
/// <param name="Provider">The LLM provider name (openrouter, ollama).</param>
/// <param name="IsCustomizable">Whether the strategy model can be customized.</param>
/// <param name="AdminOnly">Whether this strategy is admin-only.</param>
public record StrategyModelMappingEntry(
    string Strategy,
    string PrimaryModel,
    string[] FallbackModels,
    string Provider,
    bool IsCustomizable,
    bool AdminOnly);

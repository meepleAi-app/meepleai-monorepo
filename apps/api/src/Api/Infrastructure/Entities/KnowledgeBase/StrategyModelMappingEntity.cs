namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// Database entity for strategy-to-model mapping.
/// Defines which LLM models are used for each RAG strategy.
/// Issue #3438: Part of tier-strategy-model architecture.
/// </summary>
public class StrategyModelMappingEntity
{
    /// <summary>
    /// Unique identifier for the strategy-model mapping.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// RAG strategy name (e.g., "FAST", "BALANCED", "PRECISE").
    /// Must be unique.
    /// </summary>
    public string Strategy { get; set; } = default!;

    /// <summary>
    /// Primary LLM model ID for this strategy.
    /// </summary>
    public string PrimaryModel { get; set; } = default!;

    /// <summary>
    /// JSON array of fallback model IDs if primary fails.
    /// Stored as JSONB in PostgreSQL.
    /// </summary>
    public string[] FallbackModels { get; set; } = Array.Empty<string>();

    /// <summary>
    /// LLM provider name (e.g., "openrouter", "ollama").
    /// </summary>
    public string Provider { get; set; } = default!;

    /// <summary>
    /// Whether users can customize this strategy's model selection.
    /// </summary>
    public bool IsCustomizable { get; set; } = true;

    /// <summary>
    /// Whether this strategy is restricted to admin users only.
    /// </summary>
    public bool AdminOnly { get; set; }

    /// <summary>
    /// When the record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the record was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

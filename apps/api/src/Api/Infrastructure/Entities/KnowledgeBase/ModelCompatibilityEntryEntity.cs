namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// Database entity for model compatibility matrix.
/// Tracks available LLM models, their alternatives, and availability status.
/// Issue #5496: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
public class ModelCompatibilityEntryEntity
{
    /// <summary>
    /// Unique identifier for the compatibility entry.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Full model identifier (e.g., "meta-llama/llama-3.3-70b-instruct:free").
    /// </summary>
    public string ModelId { get; set; } = default!;

    /// <summary>
    /// Display name for the model (e.g., "Llama 3.3 70B").
    /// </summary>
    public string DisplayName { get; set; } = default!;

    /// <summary>
    /// LLM provider name (e.g., "openrouter", "ollama").
    /// </summary>
    public string Provider { get; set; } = default!;

    /// <summary>
    /// Ordered list of alternative model IDs. First = best alternative.
    /// Stored as JSONB in PostgreSQL.
    /// </summary>
    public string[] Alternatives { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Model context window size in tokens.
    /// </summary>
    public int ContextWindow { get; set; }

    /// <summary>
    /// Model strengths/capabilities (e.g., "reasoning", "speed", "multilingual").
    /// Stored as JSONB in PostgreSQL.
    /// </summary>
    public string[] Strengths { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether the model is currently available on its provider.
    /// Updated by ModelAvailabilityCheckJob.
    /// </summary>
    public bool IsCurrentlyAvailable { get; set; } = true;

    /// <summary>
    /// Whether the model has been marked as deprecated by the provider.
    /// </summary>
    public bool IsDeprecated { get; set; }

    /// <summary>
    /// When the model availability was last verified.
    /// </summary>
    public DateTime? LastVerifiedAt { get; set; }

    /// <summary>
    /// When the record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the record was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

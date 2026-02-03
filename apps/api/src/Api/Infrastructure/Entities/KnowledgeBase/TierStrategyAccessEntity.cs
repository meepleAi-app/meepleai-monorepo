namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// Database entity for tier-to-strategy access control.
/// Controls which user tiers can access which RAG strategies.
/// Issue #3438: Part of tier-strategy-model architecture.
/// </summary>
public class TierStrategyAccessEntity
{
    /// <summary>
    /// Unique identifier for the tier-strategy access record.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// User tier name (e.g., "User", "Editor", "Admin").
    /// Use "*" for wildcard (all strategies).
    /// </summary>
    public string Tier { get; set; } = default!;

    /// <summary>
    /// RAG strategy name (e.g., "FAST", "BALANCED", "PRECISE").
    /// Use "*" for wildcard access (Admin tier).
    /// </summary>
    public string Strategy { get; set; } = default!;

    /// <summary>
    /// Whether this tier-strategy combination is currently enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// When the record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the record was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

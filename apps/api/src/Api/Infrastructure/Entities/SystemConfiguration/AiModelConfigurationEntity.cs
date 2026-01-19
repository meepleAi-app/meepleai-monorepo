namespace Api.Infrastructure.Entities.SystemConfiguration;

/// <summary>
/// Persistence entity for AI model configurations.
/// Maps to SystemConfiguration.AiModelConfigurations table.
/// Issue #2596: Extended with tier routing and environment separation.
/// </summary>
public sealed class AiModelConfigurationEntity
{
    public Guid Id { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Issue #2596: Tier routing configuration
    /// <summary>
    /// User tier this configuration applies to. Null means global (all tiers).
    /// Stored as nullable int for database compatibility.
    /// </summary>
    public int? ApplicableTier { get; set; }

    /// <summary>
    /// Environment type: 0 = Production, 1 = Test.
    /// </summary>
    public int EnvironmentType { get; set; }

    /// <summary>
    /// Whether this is the default model for the tier/environment combination.
    /// </summary>
    public bool IsDefaultForTier { get; set; }

    // JSON Settings (JSONB) - Issue #2520: More flexible than individual columns
    public string SettingsJson { get; set; } = "{}";

    // JSON Pricing (JSONB) - Issue #2520, Migration 20260117090414
    public string PricingJson { get; set; } = "{}";

    // JSON Usage Stats (JSONB) - Issue #2520
    public string UsageJson { get; set; } = "{}";
}

namespace Api.Infrastructure.Entities.SystemConfiguration;

/// <summary>
/// Persistence entity for AI model configurations.
/// Maps to SystemConfiguration.AiModelConfigurations table.
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

    // JSON Settings (JSONB) - Issue #2520: More flexible than individual columns
    public string SettingsJson { get; set; } = "{}";

    // JSON Usage Stats (JSONB) - Issue #2520
    public string UsageJson { get; set; } = "{}";
}

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

    // Model Settings (Issue #2520)
    public int MaxTokens { get; set; }
    public decimal Temperature { get; set; }

    // Pricing (per 1M tokens in USD) (Issue #2520)
    public decimal CostPerInputToken { get; set; }
    public decimal CostPerOutputToken { get; set; }

    // Usage Tracking (Issue #2520)
    public long TotalRequests { get; set; }
    public long TotalTokensUsed { get; set; }
    public decimal TotalCostUsd { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

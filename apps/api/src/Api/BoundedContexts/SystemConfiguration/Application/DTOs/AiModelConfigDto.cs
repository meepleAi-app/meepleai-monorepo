namespace Api.BoundedContexts.SystemConfiguration.Application.DTOs;

/// <summary>
/// Issue #2520: DTO for AI model configuration
/// </summary>
public sealed record AiModelConfigDto
{
    public Guid Id { get; init; }
    public string ModelId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public int Priority { get; init; }
    public bool IsActive { get; init; }
    public bool IsPrimary { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    // JSONB properties
    public ModelSettingsDto Settings { get; init; } = new();
    public ModelPricingDto Pricing { get; init; } = new();
    public UsageStatsDto Usage { get; init; } = new();
}

public sealed record ModelSettingsDto
{
    public int MaxTokens { get; init; } = 4096;
    public double Temperature { get; init; } = 0.7;
    public double? TopP { get; init; }
    public double? FrequencyPenalty { get; init; }
    public double? PresencePenalty { get; init; }
}

public sealed record ModelPricingDto
{
    public decimal InputPricePerMillion { get; init; }
    public decimal OutputPricePerMillion { get; init; }
    public string Currency { get; init; } = "USD";
}

public sealed record UsageStatsDto
{
    public int TotalRequests { get; init; }
    public long TotalTokensUsed { get; init; }
    public decimal TotalCostUsd { get; init; }
    public DateTime? LastUsedAt { get; init; }
}

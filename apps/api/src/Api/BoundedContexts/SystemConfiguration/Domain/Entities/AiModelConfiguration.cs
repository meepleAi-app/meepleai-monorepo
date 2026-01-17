namespace Api.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Represents an AI model configuration for runtime selection.
/// Supports OpenRouter and Ollama providers with priority-based fallback.
/// </summary>
public sealed class AiModelConfiguration
{
    public Guid Id { get; private set; }
    public string ModelId { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Provider { get; private set; } = string.Empty;
    public int Priority { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Model Settings (Issue #2520)
    public int MaxTokens { get; private set; }
    public decimal Temperature { get; private set; }

    // Pricing (per 1M tokens in USD) (Issue #2520)
    public decimal CostPerInputToken { get; private set; }
    public decimal CostPerOutputToken { get; private set; }

    // Usage Tracking (Issue #2520)
    public long TotalRequests { get; private set; }
    public long TotalTokensUsed { get; private set; }
    public decimal TotalCostUsd { get; private set; }
    public DateTime? LastUsedAt { get; private set; }

    private AiModelConfiguration() { } // EF Core constructor

    private AiModelConfiguration(
        Guid id,
        string modelId,
        string displayName,
        string provider,
        int priority,
        int maxTokens,
        decimal temperature,
        decimal costPerInputToken,
        decimal costPerOutputToken,
        bool isActive = true,
        bool isPrimary = false)
    {
        Id = id;
        ModelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Priority = priority;
        MaxTokens = maxTokens;
        Temperature = temperature;
        CostPerInputToken = costPerInputToken;
        CostPerOutputToken = costPerOutputToken;
        IsActive = isActive;
        IsPrimary = isPrimary;
        CreatedAt = DateTime.UtcNow;
        // Initialize usage tracking
        TotalRequests = 0;
        TotalTokensUsed = 0;
        TotalCostUsd = 0m;
    }

    /// <summary>
    /// Factory method to create a new AI model configuration.
    /// </summary>
    public static AiModelConfiguration Create(
        string modelId,
        string displayName,
        string provider,
        int priority,
        int maxTokens = 4096,
        decimal temperature = 0.7m,
        decimal costPerInputToken = 0m,
        decimal costPerOutputToken = 0m,
        bool isActive = true,
        bool isPrimary = false)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("ModelId cannot be empty", nameof(modelId));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("DisplayName cannot be empty", nameof(displayName));

        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider cannot be empty", nameof(provider));

        if (priority < 1)
            throw new ArgumentException("Priority must be >= 1", nameof(priority));

        if (maxTokens < 512 || maxTokens > 8192)
            throw new ArgumentException("MaxTokens must be between 512 and 8192", nameof(maxTokens));

        if (temperature < 0 || temperature > 2)
            throw new ArgumentException("Temperature must be between 0 and 2", nameof(temperature));

        return new AiModelConfiguration(
            Guid.NewGuid(),
            modelId,
            displayName,
            provider,
            priority,
            maxTokens,
            temperature,
            costPerInputToken,
            costPerOutputToken,
            isActive,
            isPrimary
        );
    }

    public void UpdatePriority(int newPriority)
    {
        if (newPriority < 1)
            throw new ArgumentException("Priority must be >= 1", nameof(newPriority));

        Priority = newPriority;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPrimary(bool primary)
    {
        IsPrimary = primary;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Update model generation settings (Issue #2520)
    /// </summary>
    public void UpdateSettings(int maxTokens, decimal temperature)
    {
        if (maxTokens < 512 || maxTokens > 8192)
            throw new ArgumentException("MaxTokens must be between 512 and 8192", nameof(maxTokens));

        if (temperature < 0 || temperature > 2)
            throw new ArgumentException("Temperature must be between 0 and 2", nameof(temperature));

        MaxTokens = maxTokens;
        Temperature = temperature;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Track usage statistics after AI request (Issue #2520)
    /// </summary>
    public void TrackUsage(int tokensUsed, decimal costUsd)
    {
        if (tokensUsed < 0)
            throw new ArgumentException("TokensUsed cannot be negative", nameof(tokensUsed));

        if (costUsd < 0)
            throw new ArgumentException("CostUsd cannot be negative", nameof(costUsd));

        TotalRequests++;
        TotalTokensUsed += tokensUsed;
        TotalCostUsd += costUsd;
        LastUsedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Update pricing information (Issue #2520)
    /// </summary>
    public void UpdatePricing(decimal costPerInputToken, decimal costPerOutputToken)
    {
        if (costPerInputToken < 0)
            throw new ArgumentException("CostPerInputToken cannot be negative", nameof(costPerInputToken));

        if (costPerOutputToken < 0)
            throw new ArgumentException("CostPerOutputToken cannot be negative", nameof(costPerOutputToken));

        CostPerInputToken = costPerInputToken;
        CostPerOutputToken = costPerOutputToken;
        UpdatedAt = DateTime.UtcNow;
    }
}

using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Represents an AI model configuration for runtime selection.
/// Supports OpenRouter and Ollama providers with priority-based fallback.
/// Issue #2520: Refactored to use JSON settings for flexibility
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

    // JSON Settings (JSONB) - Issue #2520
    public ModelSettings Settings { get; private set; } = ModelSettings.Default;

    // JSON Usage Stats (JSONB) - Issue #2520
    public UsageStats Usage { get; private set; } = UsageStats.Empty;

    private AiModelConfiguration() { } // EF Core constructor

    private AiModelConfiguration(
        Guid id,
        string modelId,
        string displayName,
        string provider,
        int priority,
        ModelSettings settings,
        bool isActive = true,
        bool isPrimary = false)
    {
        Id = id;
        ModelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Priority = priority;
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Usage = UsageStats.Empty;
        IsActive = isActive;
        IsPrimary = isPrimary;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method to create a new AI model configuration.
    /// Issue #2520: Refactored to use ModelSettings value object
    /// </summary>
    public static AiModelConfiguration Create(
        string modelId,
        string displayName,
        string provider,
        int priority,
        ModelSettings settings,
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

        return new AiModelConfiguration(
            Guid.NewGuid(),
            modelId,
            displayName,
            provider,
            priority,
            settings,
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
    /// Creates new immutable Settings value object
    /// </summary>
    public void UpdateSettings(int maxTokens, decimal temperature)
    {
        var newPricing = Settings.Pricing;
        Settings = new ModelSettings(maxTokens, temperature, newPricing);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Update pricing information (Issue #2520)
    /// Creates new immutable Settings value object with updated pricing
    /// </summary>
    public void UpdatePricing(decimal inputCostPer1M, decimal outputCostPer1M)
    {
        var newPricing = new ModelPricing(inputCostPer1M, outputCostPer1M);
        Settings = new ModelSettings(Settings.MaxTokens, Settings.Temperature, newPricing);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Track usage statistics after AI request (Issue #2520)
    /// Creates new immutable UsageStats value object
    /// </summary>
    public void TrackUsage(int inputTokens, int outputTokens, decimal costUsd)
    {
        if (inputTokens < 0)
            throw new ArgumentException("InputTokens cannot be negative", nameof(inputTokens));

        if (outputTokens < 0)
            throw new ArgumentException("OutputTokens cannot be negative", nameof(outputTokens));

        if (costUsd < 0)
            throw new ArgumentException("CostUsd cannot be negative", nameof(costUsd));

        Usage = Usage.TrackRequest(inputTokens, outputTokens, costUsd);
        UpdatedAt = DateTime.UtcNow;
    }
}

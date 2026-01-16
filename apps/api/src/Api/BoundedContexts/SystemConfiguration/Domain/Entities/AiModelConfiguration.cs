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

    private AiModelConfiguration() { } // EF Core constructor

    private AiModelConfiguration(
        Guid id,
        string modelId,
        string displayName,
        string provider,
        int priority,
        bool isActive = true,
        bool isPrimary = false)
    {
        Id = id;
        ModelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Priority = priority;
        IsActive = isActive;
        IsPrimary = isPrimary;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method to create a new AI model configuration.
    /// </summary>
    public static AiModelConfiguration Create(
        string modelId,
        string displayName,
        string provider,
        int priority,
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
}

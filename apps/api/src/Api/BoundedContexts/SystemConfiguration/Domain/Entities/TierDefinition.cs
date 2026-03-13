using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Entity representing a subscription tier with its resource limits.
/// D3: Game Night Flow - tier system definitions.
/// </summary>
public class TierDefinition
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public TierLimits Limits { get; private set; } = null!;
    public string LlmModelTier { get; private set; } = null!;
    public bool IsDefault { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private TierDefinition() { }
#pragma warning restore CS8618

    public static TierDefinition Create(string name, string displayName,
        TierLimits limits, string llmModelTier, bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required", nameof(displayName));

        return new TierDefinition
        {
            Id = Guid.NewGuid(),
            Name = name.ToLowerInvariant(),
            DisplayName = displayName,
            Limits = limits,
            LlmModelTier = llmModelTier,
            IsDefault = isDefault,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void UpdateLimits(TierLimits newLimits)
    {
        Limits = newLimits;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateLlmModelTier(string tier)
    {
        LlmModelTier = tier;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        UpdatedAt = DateTime.UtcNow;
    }
}

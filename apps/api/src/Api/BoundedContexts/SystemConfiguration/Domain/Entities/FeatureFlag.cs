using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// FeatureFlag aggregate root for runtime feature toggles.
/// </summary>
internal sealed class FeatureFlag : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public bool IsEnabled { get; private set; }
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private FeatureFlag() : base() { }
#pragma warning restore CS8618

    public FeatureFlag(
        Guid id,
        string name,
        bool isEnabled = false,
        string? description = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Feature flag name cannot be empty", nameof(name));

        Name = name.Trim();
        IsEnabled = isEnabled;
        Description = description;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void Enable()
    {
        if (!IsEnabled)
        {
            IsEnabled = true;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void Disable()
    {
        if (IsEnabled)
        {
            IsEnabled = false;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void Toggle()
    {
        IsEnabled = !IsEnabled;
        UpdatedAt = DateTime.UtcNow;
    }
}

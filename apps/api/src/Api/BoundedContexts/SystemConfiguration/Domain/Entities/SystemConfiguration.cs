using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;
using SystemConfigurationAggregate = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// SystemConfiguration aggregate root representing a system configuration entry.
/// </summary>
public sealed class SystemConfiguration : AggregateRoot<Guid>
{
    public ConfigKey Key { get; private set; }
    public string Value { get; private set; }
    public string ValueType { get; private set; } // "string", "int", "bool", "json"
    public string? Description { get; private set; }
    public string Category { get; private set; }
    public bool IsActive { get; private set; }
    public bool RequiresRestart { get; private set; }
    public string Environment { get; private set; } // "Development", "Production", "All"
    public int Version { get; private set; }
    public string? PreviousValue { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public DateTime? LastToggledAt { get; private set; }

#pragma warning disable CS8618
    private SystemConfiguration() : base() { }
#pragma warning restore CS8618

    public SystemConfiguration(
        Guid id,
        ConfigKey key,
        string value,
        string valueType,
        Guid createdByUserId,
        string? description = null,
        string category = "General",
        string environment = "All",
        bool requiresRestart = false) : base(id)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be empty", nameof(value));

        if (string.IsNullOrWhiteSpace(valueType))
            throw new ArgumentException("ValueType cannot be empty", nameof(valueType));

        Key = key ?? throw new ArgumentNullException(nameof(key));
        Value = value;
        ValueType = valueType;
        Description = description;
        Category = category;
        Environment = environment;
        RequiresRestart = requiresRestart;
        IsActive = true;
        Version = 1;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        CreatedByUserId = createdByUserId;
    }

    public void UpdateValue(string newValue, Guid updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(newValue))
            throw new ArgumentException("Value cannot be empty", nameof(newValue));

        PreviousValue = Value;
        Value = newValue;
        Version++;
        UpdatedAt = DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId;
    }

    public void Activate()
    {
        if (!IsActive)
        {
            IsActive = true;
            LastToggledAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void Deactivate()
    {
        if (IsActive)
        {
            IsActive = false;
            LastToggledAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void Rollback(Guid rolledBackByUserId)
    {
        if (string.IsNullOrEmpty(PreviousValue))
            throw new InvalidOperationException("No previous value to rollback to");

        var temp = Value;
        Value = PreviousValue;
        PreviousValue = temp;
        Version++;
        UpdatedAt = DateTime.UtcNow;
        UpdatedByUserId = rolledBackByUserId;
    }
}

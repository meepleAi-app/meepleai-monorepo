using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Events;

/// <summary>
/// Domain event raised when a new configuration is created.
/// </summary>
public sealed class ConfigurationCreatedEvent : DomainEventBase
{
    public Guid ConfigurationId { get; }
    public ConfigKey Key { get; }
    public string Value { get; }
    public string ValueType { get; }
    public string Category { get; }
    public string Environment { get; }
    public bool RequiresRestart { get; }
    public Guid CreatedByUserId { get; }

    public ConfigurationCreatedEvent(
        Guid configurationId,
        ConfigKey key,
        string value,
        string valueType,
        string category,
        string environment,
        bool requiresRestart,
        Guid createdByUserId)
    {
        ConfigurationId = configurationId;
        Key = key;
        Value = value;
        ValueType = valueType;
        Category = category;
        Environment = environment;
        RequiresRestart = requiresRestart;
        CreatedByUserId = createdByUserId;
    }
}

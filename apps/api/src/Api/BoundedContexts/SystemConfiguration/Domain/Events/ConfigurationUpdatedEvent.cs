using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Events;

/// <summary>
/// Domain event raised when a configuration value is updated.
/// </summary>
internal sealed class ConfigurationUpdatedEvent : DomainEventBase
{
    public Guid ConfigurationId { get; }
    public ConfigKey Key { get; }
    public string PreviousValue { get; }
    public string NewValue { get; }
    public int Version { get; }
    public Guid UpdatedByUserId { get; }

    public ConfigurationUpdatedEvent(
        Guid configurationId,
        ConfigKey key,
        string previousValue,
        string newValue,
        int version,
        Guid updatedByUserId)
    {
        ConfigurationId = configurationId;
        Key = key;
        PreviousValue = previousValue;
        NewValue = newValue;
        Version = version;
        UpdatedByUserId = updatedByUserId;
    }
}

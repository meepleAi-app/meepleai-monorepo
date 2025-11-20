using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Events;

/// <summary>
/// Domain event raised when a configuration's active status is toggled.
/// </summary>
public sealed class ConfigurationToggledEvent : DomainEventBase
{
    public Guid ConfigurationId { get; }
    public ConfigKey Key { get; }
    public bool IsActive { get; }
    public Guid ToggledByUserId { get; }

    public ConfigurationToggledEvent(
        Guid configurationId,
        ConfigKey key,
        bool isActive,
        Guid toggledByUserId)
    {
        ConfigurationId = configurationId;
        Key = key;
        IsActive = isActive;
        ToggledByUserId = toggledByUserId;
    }
}

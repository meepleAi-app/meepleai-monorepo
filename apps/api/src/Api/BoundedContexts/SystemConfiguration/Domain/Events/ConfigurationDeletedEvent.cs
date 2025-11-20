using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Events;

/// <summary>
/// Domain event raised when a configuration is deleted.
/// </summary>
public sealed class ConfigurationDeletedEvent : DomainEventBase
{
    public Guid ConfigurationId { get; }
    public ConfigKey Key { get; }
    public string Category { get; }
    public string Environment { get; }

    public ConfigurationDeletedEvent(
        Guid configurationId,
        ConfigKey key,
        string category,
        string environment)
    {
        ConfigurationId = configurationId;
        Key = key;
        Category = category;
        Environment = environment;
    }
}

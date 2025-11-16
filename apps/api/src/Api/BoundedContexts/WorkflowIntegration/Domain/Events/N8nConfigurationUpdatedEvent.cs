using Api.BoundedContexts.WorkflowIntegration.Domain.ValueObjects;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.WorkflowIntegration.Domain.Events;

/// <summary>
/// Domain event raised when an N8n configuration is updated.
/// </summary>
public sealed class N8nConfigurationUpdatedEvent : DomainEventBase
{
    public Guid ConfigurationId { get; }
    public string? Name { get; }
    public WorkflowUrl? BaseUrl { get; }
    public WorkflowUrl? WebhookUrl { get; }
    public bool? IsActive { get; }

    public N8nConfigurationUpdatedEvent(
        Guid configurationId,
        string? name = null,
        WorkflowUrl? baseUrl = null,
        WorkflowUrl? webhookUrl = null,
        bool? isActive = null)
    {
        ConfigurationId = configurationId;
        Name = name;
        BaseUrl = baseUrl;
        WebhookUrl = webhookUrl;
        IsActive = isActive;
    }
}

using Api.BoundedContexts.WorkflowIntegration.Domain.ValueObjects;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.WorkflowIntegration.Domain.Events;

/// <summary>
/// Domain event raised when a new N8n configuration is created.
/// </summary>
public sealed class N8nConfigurationCreatedEvent : DomainEventBase
{
    public Guid ConfigurationId { get; }
    public string Name { get; }
    public WorkflowUrl BaseUrl { get; }
    public WorkflowUrl? WebhookUrl { get; }
    public bool IsActive { get; }
    public Guid CreatedByUserId { get; }

    public N8nConfigurationCreatedEvent(
        Guid configurationId,
        string name,
        WorkflowUrl baseUrl,
        WorkflowUrl? webhookUrl,
        bool isActive,
        Guid createdByUserId)
    {
        ConfigurationId = configurationId;
        Name = name;
        BaseUrl = baseUrl;
        WebhookUrl = webhookUrl;
        IsActive = isActive;
        CreatedByUserId = createdByUserId;
    }
}

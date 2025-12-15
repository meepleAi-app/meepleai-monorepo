using Api.BoundedContexts.WorkflowIntegration.Domain.Events;
using Api.BoundedContexts.WorkflowIntegration.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.WorkflowIntegration.Domain.Entities;

/// <summary>
/// N8NConfiguration aggregate root representing n8n instance configuration.
/// </summary>
internal sealed class N8NConfiguration : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public WorkflowUrl BaseUrl { get; private set; }
    public string ApiKeyEncrypted { get; private set; }
    public WorkflowUrl? WebhookUrl { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? LastTestedAt { get; private set; }
    public string? LastTestResult { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private N8NConfiguration() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new n8n configuration.
    /// </summary>
    public N8NConfiguration(
        Guid id,
        string name,
        WorkflowUrl baseUrl,
        string apiKeyEncrypted,
        Guid createdByUserId,
        WorkflowUrl? webhookUrl = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Configuration name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(apiKeyEncrypted))
            throw new ArgumentException("API key cannot be empty", nameof(apiKeyEncrypted));

        Name = name.Trim();
        BaseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        ApiKeyEncrypted = apiKeyEncrypted;
        WebhookUrl = webhookUrl;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        CreatedByUserId = createdByUserId;

        AddDomainEvent(new N8NConfigurationCreatedEvent(
            id,
            Name,
            BaseUrl,
            WebhookUrl,
            IsActive,
            createdByUserId));
    }

    /// <summary>
    /// Updates configuration details.
    /// </summary>
    public void UpdateConfiguration(
        string? name = null,
        WorkflowUrl? baseUrl = null,
        string? apiKeyEncrypted = null,
        WorkflowUrl? webhookUrl = null)
    {
        if (name != null) Name = name.Trim();
        if (baseUrl != null) BaseUrl = baseUrl;
        if (apiKeyEncrypted != null) ApiKeyEncrypted = apiKeyEncrypted;
        if (webhookUrl != null) WebhookUrl = webhookUrl;

        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new N8NConfigurationUpdatedEvent(
            Id,
            name,
            baseUrl,
            webhookUrl,
            null));
    }

    /// <summary>
    /// Activates the configuration.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the configuration.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a connection test result.
    /// </summary>
    public void RecordTestResult(bool success, string result)
    {
        LastTestedAt = DateTime.UtcNow;
        LastTestResult = result;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new N8NConfigurationTestedEvent(
            Id,
            success,
            result,
            LastTestedAt.Value));
    }
}

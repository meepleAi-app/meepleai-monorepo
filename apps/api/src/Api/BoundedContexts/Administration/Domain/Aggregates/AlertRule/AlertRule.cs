namespace Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;

/// <summary>
/// AlertRule Aggregate Root (Issue #921)
/// </summary>
internal class AlertRule
{
    public string Name { get; private set; }
    public string AlertType { get; private set; }
    public AlertSeverity Severity { get; private set; }
    public string? Description { get; private set; }
    public AlertThreshold Threshold { get; private set; }
    public AlertDuration Duration { get; private set; }
    public bool IsEnabled { get; private set; }
    public string? Metadata { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; }
    public string UpdatedBy { get; private set; }

    public Guid Id { get; private set; }

    private AlertRule(
        Guid id,
        string name,
        string alertType,
        AlertSeverity severity,
        AlertThreshold threshold,
        AlertDuration duration,
        string createdBy)
    {
        Id = id;
        Name = name;
        AlertType = alertType;
        Severity = severity;
        Threshold = threshold;
        Duration = duration;
        IsEnabled = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        CreatedBy = createdBy;
        UpdatedBy = createdBy;
    }

    public static AlertRule Create(
        string name,
        string alertType,
        AlertSeverity severity,
        AlertThreshold threshold,
        AlertDuration duration,
        string createdBy,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Alert rule name cannot be empty", nameof(name));

        var rule = new AlertRule(Guid.NewGuid(), name, alertType, severity, threshold, duration, createdBy)
        {
            Description = description
        };
        return rule;
    }

    /// <summary>
    /// Reconstitute an AlertRule from persistence (repository use only)
    /// </summary>
    public static AlertRule Reconstitute(
        Guid id,
        string name,
        string alertType,
        AlertSeverity severity,
        AlertThreshold threshold,
        AlertDuration duration,
        bool isEnabled,
        string? description,
        string? metadata,
        DateTime createdAt,
        DateTime updatedAt,
        string createdBy,
        string updatedBy)
    {
        return new AlertRule(id, name, alertType, severity, threshold, duration, createdBy)
        {
            Description = description,
            IsEnabled = isEnabled,
            Metadata = metadata,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            UpdatedBy = updatedBy
        };
    }

    public void Update(string name, AlertSeverity severity, AlertThreshold threshold, AlertDuration duration, string updatedBy, string? description = null)
    {
        Name = name;
        Severity = severity;
        Threshold = threshold;
        Duration = duration;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Enable(string updatedBy)
    {
        if (IsEnabled) return;
        IsEnabled = true;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Disable(string updatedBy)
    {
        if (!IsEnabled) return;
        IsEnabled = false;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void SetMetadata(string? metadata, string updatedBy)
    {
        Metadata = metadata;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public bool EvaluateThreshold(double metricValue) => Threshold.IsExceeded(metricValue);

    public bool ShouldTrigger(DateTime violationStartTime)
    {
        if (!IsEnabled) return false;
        var elapsed = DateTime.UtcNow - violationStartTime;
        return elapsed >= Duration.ToTimeSpan();
    }
}

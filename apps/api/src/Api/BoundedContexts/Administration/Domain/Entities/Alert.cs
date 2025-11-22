using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.Administration.Domain.Entities;

public sealed class Alert : AggregateRoot<Guid>
{
    public string AlertType { get; private set; }
    public AlertSeverity Severity { get; private set; }
    public string Message { get; private set; }
    public string? Metadata { get; private set; }
    public DateTime TriggeredAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public bool IsActive { get; private set; }

#pragma warning disable CS8618
    private Alert() : base() { }
#pragma warning restore CS8618

    public Alert(Guid id, string alertType, AlertSeverity severity, string message, string? metadata = null) : base(id)
    {
        AlertType = alertType;
        Severity = severity;
        Message = message;
        Metadata = metadata;
        TriggeredAt = DateTime.UtcNow;
        IsActive = true;
    }

    public void Resolve()
    {
        if (IsActive)
        {
            IsActive = false;
            ResolvedAt = DateTime.UtcNow;
        }
    }
}

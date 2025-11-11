using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.Administration.Domain.Entities;

public sealed class AuditLog : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public string Action { get; private set; }
    public string EntityType { get; private set; }
    public string? EntityId { get; private set; }
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string? IpAddress { get; private set; }

#pragma warning disable CS8618
    private AuditLog() : base() { }
#pragma warning restore CS8618

    public AuditLog(
        Guid id,
        Guid userId,
        string action,
        string entityType,
        string? entityId = null,
        string? oldValues = null,
        string? newValues = null,
        string? ipAddress = null) : base(id)
    {
        UserId = userId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        OldValues = oldValues;
        NewValues = newValues;
        Timestamp = DateTime.UtcNow;
        IpAddress = ipAddress;
    }
}

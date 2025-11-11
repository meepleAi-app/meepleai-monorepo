using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.Administration.Domain.Entities;

public sealed class AuditLog : AggregateRoot<Guid>
{
    public Guid? UserId { get; private set; }
    public string Action { get; private set; }
    public string Resource { get; private set; }
    public string? ResourceId { get; private set; }
    public string Result { get; private set; }
    public string? Details { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

#pragma warning disable CS8618
    private AuditLog() : base() { }
#pragma warning restore CS8618

    public AuditLog(
        Guid id,
        Guid? userId,
        string action,
        string resource,
        string result,
        string? resourceId = null,
        string? details = null,
        string? ipAddress = null,
        string? userAgent = null) : base(id)
    {
        UserId = userId;
        Action = action ?? throw new ArgumentNullException(nameof(action));
        Resource = resource ?? throw new ArgumentNullException(nameof(resource));
        Result = result ?? throw new ArgumentNullException(nameof(result));
        ResourceId = resourceId;
        Details = details;
        CreatedAt = DateTime.UtcNow;
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }
}

namespace Api.Infrastructure.Entities;

internal class AuditLogEntity
{
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid Id { get; set; } = Guid.NewGuid();
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid? UserId { get; set; }
    public string Action { get; set; } = default!;
    public string Resource { get; set; } = default!;
    public string? ResourceId { get; set; }
    public string Result { get; set; } = default!; // Success, Denied, Error
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

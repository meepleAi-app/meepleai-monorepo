namespace Api.Infrastructure.Entities;

public class AuditLogEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? UserId { get; set; }
    public string Action { get; set; } = default!;
    public string Resource { get; set; } = default!;
    public string? ResourceId { get; set; }
    public string Result { get; set; } = default!; // Success, Denied, Error
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

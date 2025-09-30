namespace Api.Infrastructure.Entities;

public class UserSessionEntity
{
    public string Id { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public string SessionToken { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
        = DateTime.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
        = null;
    public DateTimeOffset? LastSeenAt { get; set; }
        = null;
    public DateTimeOffset? RevokedAt { get; set; }
        = null;
    public string? UserAgent { get; set; }
        = null;
    public string? IpAddress { get; set; }
        = null;

    public TenantEntity Tenant { get; set; } = default!;
    public UserEntity User { get; set; } = default!;
}

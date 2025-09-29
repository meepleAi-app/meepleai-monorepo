namespace Api.Infrastructure.Entities;

public class UserSessionEntity
{
    public string Id { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public string TokenHash { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; };
    public DateTime? LastSeenAt { get; set; };
    public DateTime? RevokedAt { get; set; };
    public string? UserAgent { get; set; };
    public string? IpAddress { get; set; };

    public TenantEntity Tenant { get; set; } = default!;
    public UserEntity User { get; set; } = default!;
}

namespace Api.Infrastructure.Entities;

public class UserSessionEntity
{
    required public string Id { get; set; }
    required public string TenantId { get; set; }
    required public string UserId { get; set; }
    required public string TokenHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    required public TenantEntity Tenant { get; set; }
    required public UserEntity User { get; set; }
}


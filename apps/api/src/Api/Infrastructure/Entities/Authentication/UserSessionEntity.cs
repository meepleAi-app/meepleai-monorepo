namespace Api.Infrastructure.Entities;

/// <summary>
/// User session entity - persistence model.
/// DDD-PHASE2: Converted to Guid IDs for domain alignment.
/// </summary>
public class UserSessionEntity
{
    required public Guid Id { get; set; }
    required public Guid UserId { get; set; }
    required public string TokenHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceFingerprint { get; set; } // Issue #3677: Device tracking
    required public UserEntity User { get; set; }
}


namespace Api.Infrastructure.Entities;

/// <summary>
/// Temporary session for 2FA verification (short-lived, single-use)
/// AUTH-07: Secure temp token between password validation and 2FA verification
/// DDD-PHASE2: Converted to Guid IDs for domain alignment.
/// </summary>
public class TempSessionEntity
{
    required public Guid Id { get; set; }
    required public Guid UserId { get; set; }
    required public string TokenHash { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } // 5 minutes from creation
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }

    // Navigation
    public UserEntity User { get; set; } = null!;
}

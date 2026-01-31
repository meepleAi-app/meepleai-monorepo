namespace Api.Infrastructure.Entities.Authentication;

/// <summary>
/// Used TOTP code entity - tracks used TOTP codes to prevent replay attacks.
/// SECURITY: Issue #1787 - TOTP Replay Attack Prevention (OWASP ASVS 2.8.3)
/// Pattern: Nonce validation with automatic TTL cleanup.
/// </summary>
public class UsedTotpCodeEntity
{
    /// <summary>
    /// Primary key.
    /// </summary>
    required public Guid Id { get; set; }

    /// <summary>
    /// User ID who used this TOTP code.
    /// </summary>
    required public Guid UserId { get; set; }

    /// <summary>
    /// PBKDF2 hash of the TOTP code (210k iterations, same as PasswordHashingService).
    /// SECURITY: Hashed to prevent plaintext storage of sensitive OTP codes.
    /// </summary>
    required public string CodeHash { get; set; }

    /// <summary>
    /// TOTP time step when the code was used (for forensic analysis).
    /// </summary>
    public long TimeStep { get; set; }

    /// <summary>
    /// Timestamp when the code was used.
    /// </summary>
    public DateTime UsedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Expiration timestamp for automatic cleanup (TTL = 2 minutes).
    /// PERFORMANCE: Background job deletes expired entries hourly.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    // Navigation property
    public UserEntity User { get; set; } = null!;
}

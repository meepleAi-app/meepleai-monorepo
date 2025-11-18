namespace Api.Infrastructure.Entities;

/// <summary>
/// User entity - persistence model.
/// DDD-PHASE2: Converted to Guid IDs and string Role for domain alignment.
/// </summary>
public class UserEntity
{
    required public Guid Id { get; set; }
    required public string Email { get; set; }
    public string? DisplayName { get; set; }
    public string? PasswordHash { get; set; } // Nullable for OAuth-only users
    public string Role { get; set; } = "user"; // DDD-PHASE2: Changed from enum to string
    public DateTime CreatedAt { get; set; }

    // Two-Factor Authentication
    public string? TotpSecretEncrypted { get; set; }
    public bool IsTwoFactorEnabled { get; set; } = false;
    public DateTime? TwoFactorEnabledAt { get; set; }

    // Navigation properties
    public ICollection<UserSessionEntity> Sessions { get; set; } = new List<UserSessionEntity>();
    public ICollection<UserBackupCodeEntity> BackupCodes { get; set; } = new List<UserBackupCodeEntity>();
    public ICollection<OAuthAccountEntity> OAuthAccounts { get; set; } = new List<OAuthAccountEntity>();
}

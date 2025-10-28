namespace Api.Infrastructure.Entities;

public class UserEntity
{
    required public string Id { get; set; }
    required public string Email { get; set; }
    public string? DisplayName { get; set; }
    required public string PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }

    // Two-Factor Authentication
    public string? TotpSecretEncrypted { get; set; }
    public bool IsTwoFactorEnabled { get; set; } = false;
    public DateTime? TwoFactorEnabledAt { get; set; }

    // Navigation properties
    public ICollection<UserSessionEntity> Sessions { get; set; } = new List<UserSessionEntity>();
    public ICollection<UserBackupCodeEntity> BackupCodes { get; set; } = new List<UserBackupCodeEntity>();
}

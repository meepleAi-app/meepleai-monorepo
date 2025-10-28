namespace Api.Infrastructure.Entities;

public class UserBackupCodeEntity
{
    required public string Id { get; set; }
    required public string UserId { get; set; }
    required public string CodeHash { get; set; }
    public bool IsUsed { get; set; } = false;
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public UserEntity User { get; set; } = null!;
}

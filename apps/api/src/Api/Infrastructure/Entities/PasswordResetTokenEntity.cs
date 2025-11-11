namespace Api.Infrastructure.Entities;

/// <summary>
/// Password reset token entity - persistence model.
/// DDD-PHASE2: Converted to Guid IDs for domain alignment.
/// </summary>
public class PasswordResetTokenEntity
{
    required public Guid Id { get; set; }
    required public Guid UserId { get; set; }
    required public string TokenHash { get; set; }
    required public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    required public DateTime CreatedAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public UserEntity User { get; set; } = null!;
}

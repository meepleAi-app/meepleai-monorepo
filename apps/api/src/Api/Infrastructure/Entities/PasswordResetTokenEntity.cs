namespace Api.Infrastructure.Entities;

public class PasswordResetTokenEntity
{
    required public string Id { get; set; }
    required public string UserId { get; set; }
    required public string TokenHash { get; set; }
    required public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    required public DateTime CreatedAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public UserEntity User { get; set; } = null!;
}

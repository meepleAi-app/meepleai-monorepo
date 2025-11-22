namespace Api.Infrastructure.Entities;

/// <summary>
/// User backup code entity - persistence model.
/// DDD-PHASE2: Converted to Guid IDs for domain alignment.
/// </summary>
public class UserBackupCodeEntity
{
    required public Guid Id { get; set; }
    required public Guid UserId { get; set; }
    required public string CodeHash { get; set; }
    public bool IsUsed { get; set; } = false;
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public UserEntity User { get; set; } = null!;
}

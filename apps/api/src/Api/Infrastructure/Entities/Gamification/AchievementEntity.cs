namespace Api.Infrastructure.Entities.Gamification;

/// <summary>
/// Achievement persistence entity.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
public class AchievementEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public int Points { get; set; }
    public int Rarity { get; set; }
    public int Category { get; set; }
    public int Threshold { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<UserAchievementEntity> UserAchievements { get; set; } = new List<UserAchievementEntity>();
}

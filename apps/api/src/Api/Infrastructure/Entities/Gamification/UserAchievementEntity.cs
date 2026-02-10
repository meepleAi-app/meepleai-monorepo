using Api.Infrastructure.Entities.Authentication;

namespace Api.Infrastructure.Entities.Gamification;

/// <summary>
/// UserAchievement persistence entity.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
public class UserAchievementEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid AchievementId { get; set; }
    public int Progress { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public UserEntity? User { get; set; }
    public AchievementEntity? Achievement { get; set; }
}

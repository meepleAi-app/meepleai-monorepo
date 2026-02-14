namespace Api.BoundedContexts.Administration.Domain.Entities;

/// <summary>
/// User's unlocked achievements.
/// Issue #4314: Achievement System.
/// </summary>
internal sealed class UserAchievement
{
    public Guid UserId { get; private set; }
    public Guid AchievementId { get; private set; }
    public DateTime UnlockedAt { get; private set; }

    private UserAchievement() { }

    public static UserAchievement Create(Guid userId, Guid achievementId)
    {
        return new UserAchievement
        {
            UserId = userId,
            AchievementId = achievementId,
            UnlockedAt = DateTime.UtcNow
        };
    }
}

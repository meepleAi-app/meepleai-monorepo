using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.Gamification.Domain.Entities;

/// <summary>
/// Entity tracking a user's progress toward or unlock of an achievement.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
internal sealed class UserAchievement : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Guid AchievementId { get; private set; }

    /// <summary>
    /// Progress toward achievement (0-100 percentage).
    /// 100 means unlocked.
    /// </summary>
    public int Progress { get; private set; }

    /// <summary>
    /// When the achievement was unlocked (null if not yet unlocked).
    /// </summary>
    public DateTime? UnlockedAt { get; private set; }

    /// <summary>
    /// When tracking started.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Last time progress was updated.
    /// </summary>
    public DateTime? UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private UserAchievement() : base() { }
#pragma warning restore CS8618

    private UserAchievement(Guid id, Guid userId, Guid achievementId)
        : base(id)
    {
        UserId = userId != Guid.Empty
            ? userId
            : throw new ArgumentException("UserId cannot be empty", nameof(userId));
        AchievementId = achievementId != Guid.Empty
            ? achievementId
            : throw new ArgumentException("AchievementId cannot be empty", nameof(achievementId));
        Progress = 0;
        UnlockedAt = null;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = null;
    }

    /// <summary>
    /// Factory method to start tracking an achievement for a user.
    /// </summary>
    public static UserAchievement Create(Guid userId, Guid achievementId)
    {
        return new UserAchievement(Guid.NewGuid(), userId, achievementId);
    }

    /// <summary>
    /// Reconstitutes a UserAchievement from persistence data.
    /// </summary>
    internal static UserAchievement Reconstitute(
        Guid id,
        Guid userId,
        Guid achievementId,
        int progress,
        DateTime? unlockedAt,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        var ua = new UserAchievement(id, userId, achievementId);
        ua.Progress = progress;
        ua.UnlockedAt = unlockedAt;
        ua.CreatedAt = createdAt;
        ua.UpdatedAt = updatedAt;
        return ua;
    }

    /// <summary>
    /// Updates progress percentage (0-100).
    /// Automatically unlocks when progress reaches 100.
    /// </summary>
    public bool UpdateProgress(int progressValue)
    {
        if (UnlockedAt.HasValue) return false; // Already unlocked

        Progress = Math.Clamp(progressValue, 0, 100);
        UpdatedAt = DateTime.UtcNow;

        if (Progress >= 100)
        {
            UnlockedAt = DateTime.UtcNow;
            return true; // Newly unlocked
        }

        return false;
    }

    /// <summary>
    /// Whether this achievement has been unlocked.
    /// </summary>
    public bool IsUnlocked => UnlockedAt.HasValue;
}

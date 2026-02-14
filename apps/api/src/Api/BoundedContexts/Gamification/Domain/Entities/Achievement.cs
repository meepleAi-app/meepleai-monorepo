using Api.BoundedContexts.Gamification.Domain.Enums;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.Gamification.Domain.Entities;

/// <summary>
/// Aggregate root representing an achievement definition.
/// Defines what users can unlock based on activity rules.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
internal sealed class Achievement : AggregateRoot<Guid>
{
    /// <summary>
    /// Unique code identifier (e.g., "STREAK_7_DAYS").
    /// </summary>
    public string Code { get; private set; }

    /// <summary>
    /// Display name (e.g., "Giocatore Costante").
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Detailed description of how to unlock.
    /// </summary>
    public string Description { get; private set; }

    /// <summary>
    /// URL or path to the achievement icon.
    /// </summary>
    public string IconUrl { get; private set; }

    /// <summary>
    /// Points awarded when unlocked.
    /// </summary>
    public int Points { get; private set; }

    /// <summary>
    /// Rarity tier of this achievement.
    /// </summary>
    public AchievementRarity Rarity { get; private set; }

    /// <summary>
    /// Category for grouping.
    /// </summary>
    public AchievementCategory Category { get; private set; }

    /// <summary>
    /// Threshold value for rule evaluation (e.g., 7 for 7-day streak, 100 for collector).
    /// </summary>
    public int Threshold { get; private set; }

    /// <summary>
    /// Whether this achievement is currently active and evaluable.
    /// </summary>
    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618
    private Achievement() : base() { }
#pragma warning restore CS8618

    private Achievement(
        Guid id,
        string code,
        string name,
        string description,
        string iconUrl,
        int points,
        AchievementRarity rarity,
        AchievementCategory category,
        int threshold)
        : base(id)
    {
        Code = !string.IsNullOrWhiteSpace(code)
            ? code
            : throw new ArgumentException("Code cannot be empty", nameof(code));
        Name = !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException("Name cannot be empty", nameof(name));
        Description = !string.IsNullOrWhiteSpace(description)
            ? description
            : throw new ArgumentException("Description cannot be empty", nameof(description));
        IconUrl = iconUrl ?? string.Empty;
        Points = points > 0
            ? points
            : throw new ArgumentOutOfRangeException(nameof(points), "Points must be positive");
        Rarity = rarity;
        Category = category;
        Threshold = threshold > 0
            ? threshold
            : throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be positive");
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method to create a new achievement definition.
    /// </summary>
    public static Achievement Create(
        string code,
        string name,
        string description,
        string iconUrl,
        int points,
        AchievementRarity rarity,
        AchievementCategory category,
        int threshold)
    {
        return new Achievement(
            Guid.NewGuid(),
            code, name, description, iconUrl,
            points, rarity, category, threshold);
    }

    /// <summary>
    /// Reconstitutes an achievement from persistence data.
    /// </summary>
    internal static Achievement Reconstitute(
        Guid id,
        string code,
        string name,
        string description,
        string iconUrl,
        int points,
        AchievementRarity rarity,
        AchievementCategory category,
        int threshold,
        bool isActive,
        DateTime createdAt)
    {
        var achievement = new Achievement(
            id, code, name, description, iconUrl,
            points, rarity, category, threshold);
        achievement.IsActive = isActive;
        achievement.CreatedAt = createdAt;
        return achievement;
    }

    /// <summary>
    /// Deactivates this achievement so it's no longer evaluated.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Reactivates a deactivated achievement.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }
}

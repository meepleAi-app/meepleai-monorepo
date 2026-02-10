namespace Api.BoundedContexts.Gamification.Application.DTOs;

/// <summary>
/// DTO representing an achievement with user unlock status.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
public sealed class AchievementDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public int Points { get; set; }
    public string Rarity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Threshold { get; set; }

    /// <summary>
    /// User's progress toward this achievement (0-100). Null if not tracking.
    /// </summary>
    public int? Progress { get; set; }

    /// <summary>
    /// Whether the user has unlocked this achievement.
    /// </summary>
    public bool IsUnlocked { get; set; }

    /// <summary>
    /// When the user unlocked it (null if not unlocked).
    /// </summary>
    public DateTime? UnlockedAt { get; set; }
}

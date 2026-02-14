namespace Api.BoundedContexts.Gamification.Application.DTOs;

/// <summary>
/// DTO for recently unlocked achievements.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
public sealed class RecentAchievementDto
{
    public Guid AchievementId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public int Points { get; set; }
    public string Rarity { get; set; } = string.Empty;
    public DateTime UnlockedAt { get; set; }
}

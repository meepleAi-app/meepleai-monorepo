namespace Api.BoundedContexts.Administration.Application.DTOs;

public record AchievementDto(
    Guid Id,
    string Title,
    string Description,
    string Icon,
    string Category,
    string Rarity,
    bool IsUnlocked,
    DateTime? UnlockedAt = null
);

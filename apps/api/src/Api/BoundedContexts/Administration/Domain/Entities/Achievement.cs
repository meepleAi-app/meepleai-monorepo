using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.Administration.Domain.Entities;

/// <summary>
/// Achievement definition with unlock conditions.
/// Issue #4314: Achievement System.
/// </summary>
internal sealed class Achievement : Entity<Guid>
{
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Icon { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Rarity { get; private set; } = string.Empty;
    public string ConditionJson { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private Achievement() { }

    public static Achievement Create(
        string title,
        string description,
        string icon,
        string category,
        string rarity,
        string conditionJson)
    {
        return new Achievement
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            Icon = icon,
            Category = category,
            Rarity = rarity,
            ConditionJson = conditionJson,
            CreatedAt = DateTime.UtcNow
        };
    }
}

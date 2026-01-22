namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// EF Core persistence entity for Badge entity.
/// Badge definitions for gamification system.
/// ISSUE-2731
/// </summary>
public class BadgeEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public int Tier { get; set; }
    public int Category { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
    public string RequirementJson { get; set; } = string.Empty; // JSON serialized BadgeRequirement
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

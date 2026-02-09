namespace Api.Infrastructure.Entities;

/// <summary>
/// EF Core entity for rule conflict FAQ persistence.
/// Issue #3761: Arbitro Agent Conflict Resolution FAQ System.
/// </summary>
public class RuleConflictFAQEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public int ConflictType { get; set; } // Maps to ConflictType enum
    public string Pattern { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public int Priority { get; set; } = 5; // 1-10 scale
    public int UsageCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public GameEntity? Game { get; set; }
}

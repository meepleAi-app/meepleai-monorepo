using Api.Infrastructure.Entities.UserLibrary;

namespace Api.Infrastructure.Entities.GameToolkit;

/// <summary>
/// Persistence entity for GameToolkit aggregate.
/// Tool configs stored as JSONB columns for flexibility.
/// </summary>
public class GameToolkitEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? GameId { get; set; }
    public Guid? PrivateGameId { get; set; }
    public string Name { get; set; } = default!;
    public int Version { get; set; } = 1;
    public Guid CreatedByUserId { get; set; }
    public bool IsPublished { get; set; }
    public bool OverridesTurnOrder { get; set; }
    public bool OverridesScoreboard { get; set; }
    public bool OverridesDiceSet { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // JSONB columns for tool configs
    public string? DiceToolsJson { get; set; }
    public string? CardToolsJson { get; set; }
    public string? TimerToolsJson { get; set; }
    public string? CounterToolsJson { get; set; }

    // JSONB columns for templates
    public string? ScoringTemplateJson { get; set; }
    public string? TurnTemplateJson { get; set; }
    public string? StateTemplate { get; set; }
    public string? AgentConfig { get; set; }

    // Template marketplace
    public int TemplateStatus { get; set; }
    public bool IsTemplate { get; set; }
    public string? ReviewNotes { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // Concurrency
    public byte[] RowVersion { get; set; } = default!;

    // Navigation properties
    public GameEntity? Game { get; set; }
    public PrivateGameEntity? PrivateGame { get; set; }
}

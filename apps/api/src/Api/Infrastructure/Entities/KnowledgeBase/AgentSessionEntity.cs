namespace Api.Infrastructure.Entities;

/// <summary>
/// Infrastructure entity for AgentSession.
/// Maps KnowledgeBase.Domain.Entities.AgentSession to database schema.
/// </summary>
public class AgentSessionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AgentDefinitionId { get; set; }
    public Guid GameSessionId { get; set; }
    public Guid UserId { get; set; }
    public Guid GameId { get; set; }
    public string CurrentGameStateJson { get; set; } = "{}";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public GameSessionEntity GameSession { get; set; } = default!;
    public UserEntity User { get; set; } = default!;
    public GameEntity Game { get; set; } = default!;
}

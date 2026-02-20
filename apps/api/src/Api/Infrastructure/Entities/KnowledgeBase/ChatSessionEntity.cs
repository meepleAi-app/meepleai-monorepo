namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// EF Core entity for chat session persistence.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
public class ChatSessionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid GameId { get; set; }
    public Guid? UserLibraryEntryId { get; set; }
    public Guid? AgentSessionId { get; set; }
    public Guid? AgentId { get; set; }           // Custom agent (user-owned AgentDefinition)
    public string? AgentType { get; set; }       // System agent type: auto|tutor|arbitro|decisore
    public string? AgentName { get; set; }       // Display name for grouping (copied on create)
    public string? Title { get; set; }
    public string AgentConfigJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    public bool IsArchived { get; set; }

    // Messages stored as JSON array for efficient storage
    public string MessagesJson { get; set; } = "[]";

    // Navigation properties
    public UserEntity? User { get; set; }
    public GameEntity? Game { get; set; }
}

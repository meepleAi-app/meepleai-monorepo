namespace Api.Infrastructure.Entities;

/// <summary>
/// EF Core entity for chat thread persistence (DDD KnowledgeBase context).
/// Represents a conversation thread with Q&amp;A history.
/// </summary>
public class ChatThreadEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? GameId { get; set; }
    public Guid? AgentId { get; set; } // Issue #2030 - Track which agent was used
    public string? AgentType { get; set; } // Issue #4362 - tutor/arbitro/decisore/auto
    public string? Title { get; set; }
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    // Messages stored as JSON array
    public string MessagesJson { get; set; } = "[]";

    // Issue #5259: Progressive conversation summary for sliding window strategy
    public string? ConversationSummary { get; set; }
    public int LastSummarizedMessageCount { get; set; }

    /// <summary>
    /// JSON-serialized list of VectorDocument IDs selected for RAG.
    /// Null = use all VectorDocuments for the game (default).
    /// </summary>
    public string? SelectedKnowledgeBaseIdsJson { get; set; }

    // Navigation properties
    public UserEntity? User { get; set; }
    public GameEntity? Game { get; set; }
}

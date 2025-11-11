namespace Api.Infrastructure.Entities;

/// <summary>
/// EF Core entity for chat thread persistence (DDD KnowledgeBase context).
/// Represents a conversation thread with Q&A history.
/// </summary>
public class ChatThreadEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? GameId { get; set; }
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    // Messages stored as JSON array
    public string MessagesJson { get; set; } = "[]";

    // Navigation property (optional)
    public GameEntity? Game { get; set; }
}

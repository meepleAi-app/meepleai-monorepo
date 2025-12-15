namespace Api.Infrastructure.Entities;

/// <summary>
/// EF Core entity for chat thread persistence (DDD KnowledgeBase context).
/// Represents a conversation thread with Q&A history.
/// </summary>
internal class ChatThreadEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? GameId { get; set; }
    public string? Title { get; set; }
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    // Messages stored as JSON array
    public string MessagesJson { get; set; } = "[]";

    // Navigation properties
    public UserEntity? User { get; set; }
    public GameEntity? Game { get; set; }
}

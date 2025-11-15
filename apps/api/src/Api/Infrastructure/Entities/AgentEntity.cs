namespace Api.Infrastructure.Entities;

public class AgentEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string Type { get; set; } = default!; // AgentType.Value
    public string StrategyName { get; set; } = default!;
    public string StrategyParametersJson { get; set; } = "{}"; // JSON serialized Dictionary<string, object>
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastInvokedAt { get; set; }
    public int InvocationCount { get; set; }

    // TODO Issue #866: DEPRECATED - For backward compatibility with legacy ChatService only
    // These properties are NOT part of the DDD Agent aggregate and should not be used
    [Obsolete("Legacy property for ChatService compatibility only")]
    public Guid? GameId { get; set; }

    [Obsolete("Legacy property for ChatService compatibility only - use Type instead")]
    public string? Kind { get; set; }

    [Obsolete("Legacy collection for ChatService compatibility only")]
    public ICollection<ChatEntity> Chats { get; set; } = new List<ChatEntity>();
}
namespace Api.Infrastructure.Entities;

public class AgentEntity
{
    public string Id { get; set; } = default!;
    public string GameId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Kind { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public GameEntity Game { get; set; } = default!;
    public ICollection<ChatEntity> Chats { get; set; } = new List<ChatEntity>();
}

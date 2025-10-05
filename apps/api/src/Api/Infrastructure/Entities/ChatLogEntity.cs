namespace Api.Infrastructure.Entities;

public class ChatLogEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChatId { get; set; }
        = Guid.Empty;
    public string Level { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string? MetadataJson { get; set; } = null;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ChatEntity Chat { get; set; } = default!;
}

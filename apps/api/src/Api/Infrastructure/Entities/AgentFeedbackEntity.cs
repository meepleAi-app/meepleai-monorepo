namespace Api.Infrastructure.Entities;

public class AgentFeedbackEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MessageId { get; set; } = default!;
    public string Endpoint { get; set; } = default!;
    public string? GameId { get; set; }
    public string UserId { get; set; } = default!;
    public string Outcome { get; set; } = default!; // "helpful" | "not-helpful"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

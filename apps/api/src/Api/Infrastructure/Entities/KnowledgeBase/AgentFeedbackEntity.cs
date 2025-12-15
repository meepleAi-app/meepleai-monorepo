namespace Api.Infrastructure.Entities;

internal class AgentFeedbackEntity
{
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid Id { get; set; } = Guid.NewGuid();
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid MessageId { get; set; }
    public string Endpoint { get; set; } = default!;
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid? GameId { get; set; }
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid UserId { get; set; }
    public string Outcome { get; set; } = default!; // "helpful" | "not-helpful"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

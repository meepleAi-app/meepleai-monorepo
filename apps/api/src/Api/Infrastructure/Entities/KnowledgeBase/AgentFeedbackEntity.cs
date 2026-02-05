namespace Api.Infrastructure.Entities;

public class AgentFeedbackEntity
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
    /// <summary>
    /// Optional feedback comment from user, typically provided on negative feedback.
    /// Issue #3352: AI Response Feedback System
    /// </summary>
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

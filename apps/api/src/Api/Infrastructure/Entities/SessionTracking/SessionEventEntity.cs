namespace Api.Infrastructure.Entities.SessionTracking;

/// <summary>
/// Issue #276: Unified session event timeline (Session Diary).
/// </summary>
public class SessionEventEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string EventType { get; set; } = default!;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Payload { get; set; }
    public Guid? CreatedBy { get; set; }
    public string? Source { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public SessionEntity Session { get; set; } = default!;
}

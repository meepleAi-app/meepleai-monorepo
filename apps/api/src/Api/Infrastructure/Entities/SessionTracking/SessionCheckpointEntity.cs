namespace Api.Infrastructure.Entities.SessionTracking;

/// <summary>
/// Issue #278: Deep save/checkpoint for session state.
/// </summary>
public class SessionCheckpointEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Name { get; set; } = default!;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string SnapshotData { get; set; } = "{}";
    public int DiaryEventCount { get; set; }
    public Guid? CreatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public SessionEntity Session { get; set; } = default!;
}

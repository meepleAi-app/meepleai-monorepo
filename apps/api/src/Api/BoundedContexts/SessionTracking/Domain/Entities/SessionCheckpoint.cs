using System.ComponentModel.DataAnnotations;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Domain entity representing a deep save/checkpoint of session state.
/// Issue #278 - Session Checkpoint / Deep Save
/// </summary>
public class SessionCheckpoint
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }

    [MaxLength(200)]
    public string Name { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; private set; }
    public string SnapshotData { get; private set; } = "{}";
    public int DiaryEventCount { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private SessionCheckpoint() { }

    public static SessionCheckpoint Create(
        Guid sessionId, string name, Guid createdBy,
        string snapshotData, int diaryEventCount)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session ID cannot be empty.", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        if (name.Length > 200)
            throw new ArgumentException("Name must be 200 characters or fewer.", nameof(name));
        if (createdBy == Guid.Empty)
            throw new ArgumentException("CreatedBy cannot be empty.", nameof(createdBy));
        if (string.IsNullOrWhiteSpace(snapshotData))
            throw new ArgumentException("Snapshot data cannot be empty.", nameof(snapshotData));
        if (diaryEventCount < 0)
            throw new ArgumentException("Diary event count cannot be negative.", nameof(diaryEventCount));

        return new SessionCheckpoint
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            SnapshotData = snapshotData,
            DiaryEventCount = diaryEventCount,
            IsDeleted = false
        };
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }
}

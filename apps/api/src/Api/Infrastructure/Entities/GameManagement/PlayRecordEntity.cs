using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.Authentication;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// Infrastructure entity for PlayRecord aggregate.
/// Maps domain PlayRecord to database table.
/// </summary>
public class PlayRecordEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Game Association (Optional)
    public Guid? GameId { get; set; }
    public string GameName { get; set; } = default!;
    public SharedGameEntity? Game { get; set; }

    // Ownership & Permissions
    public Guid CreatedByUserId { get; set; }
    public UserEntity? CreatedByUser { get; set; }
    public int Visibility { get; set; }  // 0=Private, 1=Group
    public Guid? GroupId { get; set; }

    // Session Metadata
    public DateTime SessionDate { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public int Status { get; set; }  // 0=Planned, 1=InProgress, 2=Completed, 3=Archived
    public string? Notes { get; set; }
    public string? Location { get; set; }

    // Share token (#2437-2, GameNightPlaylist pattern)
    public string? ShareToken { get; set; }
    public bool IsShared { get; set; }

    // Scoring Configuration (stored as JSON)
    public string ScoringConfigJson { get; set; } = default!;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Soft Delete (issue #2439)
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Optional source domain event id used to dedupe at the DB level (issue #1938 / CF-2).
    /// UNIQUE partial — only when not null.
    /// </summary>
    public Guid? SourceEventId { get; set; }

    // Optimistic concurrency via PostgreSQL's xmin system column (ADR-060).
    // Replaces the silently-disabled byte[] RowVersion (no trigger populated it).
    // Server-owned: Postgres assigns xmin = transaction-id-of-last-write per row.
    // #2436 PR-B / #2437-1.
    public uint Xmin { get; set; }

    // Navigation Properties
    public ICollection<RecordPlayerEntity> Players { get; set; } = new List<RecordPlayerEntity>();
    public ICollection<PlayRecordPhotoEntity> Photos { get; set; } = new List<PlayRecordPhotoEntity>(); // #2436 PR-B
}

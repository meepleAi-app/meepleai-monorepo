namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// Persistence POCO for PlayRecordVersion.
/// Maps domain PlayRecordVersion entity to the play_record_versions table.
/// #2437-3: version history + restore for play records.
/// </summary>
public class PlayRecordVersionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayRecordId { get; set; }

    /// <summary>Monotonically increasing version counter per PlayRecord.</summary>
    public int VersionNumber { get; set; }

    // Snapshot of the three editable fields at the time of the update.
    public DateTime SessionDate { get; set; }
    public string? Notes { get; set; }
    public string? Location { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }

    // Navigation property
    public PlayRecordEntity? PlayRecord { get; set; }
}

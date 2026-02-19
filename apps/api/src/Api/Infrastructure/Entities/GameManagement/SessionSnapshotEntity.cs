using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// EF Core persistence entity for SessionSnapshot.
/// Stores delta-based snapshots with periodic full-state checkpoints.
/// Issue #4755: SessionSnapshot - Delta-based History + State Reconstruction.
/// </summary>
public class SessionSnapshotEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public int SnapshotIndex { get; set; }
    public int TriggerType { get; set; }

    [MaxLength(500)]
    public string? TriggerDescription { get; set; }

    /// <summary>
    /// For checkpoints: full JSON state.
    /// For non-checkpoints: JSON Patch operations (RFC 6902 format).
    /// </summary>
    public string DeltaDataJson { get; set; } = "{}";

    public bool IsCheckpoint { get; set; }
    public int TurnIndex { get; set; }
    public int? PhaseIndex { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByPlayerId { get; set; }
}

using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;

/// <summary>
/// Represents a snapshot of session state at a specific point in time.
/// Uses delta-based JSON Patch storage with periodic full-state checkpoints.
/// Checkpoint every 10 snapshots; state reconstruction via delta application.
/// Issue #4755: SessionSnapshot - Delta-based History + State Reconstruction.
/// </summary>
#pragma warning disable MA0049
internal sealed class SessionSnapshot : Entity<Guid>
#pragma warning restore MA0049
{
    private const int CheckpointInterval = 10;

    public Guid SessionId { get; private set; }
    public int SnapshotIndex { get; private set; }
    public SnapshotTrigger TriggerType { get; private set; }
    public string? TriggerDescription { get; private set; }
    public string DeltaDataJson { get; private set; }
    public bool IsCheckpoint { get; private set; }
    public int TurnIndex { get; private set; }
    public int? PhaseIndex { get; private set; }
    public DateTime Timestamp { get; private set; }
    public Guid? CreatedByPlayerId { get; private set; }

    /// <summary>
    /// Creates a new session snapshot.
    /// </summary>
    internal SessionSnapshot(
        Guid id,
        Guid sessionId,
        int snapshotIndex,
        SnapshotTrigger triggerType,
        string? triggerDescription,
        string deltaDataJson,
        bool isCheckpoint,
        int turnIndex,
        int? phaseIndex,
        Guid? createdByPlayerId) : base(id)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("SessionId cannot be empty", nameof(sessionId));
        if (snapshotIndex < 0)
            throw new ArgumentException("SnapshotIndex cannot be negative", nameof(snapshotIndex));
        if (turnIndex < 0)
            throw new ArgumentException("TurnIndex cannot be negative", nameof(turnIndex));

        SessionId = sessionId;
        SnapshotIndex = snapshotIndex;
        TriggerType = triggerType;
        TriggerDescription = triggerDescription?.Trim();
        DeltaDataJson = string.IsNullOrEmpty(deltaDataJson) ? "{}" : deltaDataJson;
        IsCheckpoint = isCheckpoint;
        TurnIndex = turnIndex;
        PhaseIndex = phaseIndex;
        CreatedByPlayerId = createdByPlayerId;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Determines if a given snapshot index should be a checkpoint (full state).
    /// Snapshot 0 is always a checkpoint; then every CheckpointInterval snapshots.
    /// </summary>
    internal static bool ShouldBeCheckpoint(int snapshotIndex)
        => snapshotIndex % CheckpointInterval == 0;

    /// <summary>
    /// Gets the nearest checkpoint index at or before the given snapshot index.
    /// </summary>
    internal static int GetNearestCheckpointIndex(int snapshotIndex)
        => snapshotIndex / CheckpointInterval * CheckpointInterval;
}

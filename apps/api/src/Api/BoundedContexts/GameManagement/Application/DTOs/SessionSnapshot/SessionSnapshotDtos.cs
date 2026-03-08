using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;

namespace Api.BoundedContexts.GameManagement.Application.DTOs.SessionSnapshot;

/// <summary>
/// DTO for SessionSnapshot entity (metadata only, no state data).
/// </summary>
internal sealed record SessionSnapshotDto(
    Guid Id,
    Guid SessionId,
    int SnapshotIndex,
    SnapshotTrigger TriggerType,
    string? TriggerDescription,
    bool IsCheckpoint,
    int TurnIndex,
    int? PhaseIndex,
    DateTime Timestamp,
    Guid? CreatedByPlayerId,
    int AttachmentCount = 0);

/// <summary>
/// DTO for a reconstructed session state at a given snapshot index.
/// </summary>
internal sealed record ReconstructedStateDto(
    int SnapshotIndex,
    int TurnIndex,
    int? PhaseIndex,
    DateTime Timestamp,
    JsonElement State);

/// <summary>
/// Request DTO for creating a manual snapshot.
/// </summary>
internal sealed record CreateManualSnapshotRequest(string? Description);

using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Command to restore a play record to a previous version snapshot (creator-only).
/// Snapshots the current state first so the restore is itself undo-able.
/// Issue #2437-3: version history + restore.
/// </summary>
internal record RestorePlayRecordVersionCommand(
    Guid RecordId,
    int VersionNumber,
    Guid UserId) : ICommand;

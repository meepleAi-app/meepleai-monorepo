using Api.BoundedContexts.GameManagement.Application.DTOs.SessionSnapshot;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;

/// <summary>
/// Restores session state from a specific snapshot.
/// Before restoring, auto-creates a "Pre-restore" snapshot of the current state.
/// Issue #5581: Auto-snapshot on Pause + snapshot history.
/// </summary>
internal sealed record RestoreSessionSnapshotCommand(
    Guid SessionId,
    int SnapshotIndex) : ICommand<SessionSnapshotDto>;

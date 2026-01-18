using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to restore game state from a snapshot.
/// Issue #2403: GameSessionState Entity (restore for undo)
/// </summary>
/// <param name="SessionStateId">ID of the game session state</param>
/// <param name="SnapshotId">ID of the snapshot to restore from</param>
public sealed record RestoreStateSnapshotCommand(
    Guid SessionStateId,
    Guid SnapshotId) : ICommand<GameSessionStateDto>;

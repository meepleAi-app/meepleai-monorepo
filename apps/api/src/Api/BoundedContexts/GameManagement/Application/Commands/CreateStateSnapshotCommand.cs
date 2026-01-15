using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to create a snapshot of the current game state.
/// Issue #2403: GameSessionState Entity (snapshots for undo/history)
/// </summary>
/// <param name="SessionStateId">ID of the game session state</param>
/// <param name="TurnNumber">Turn number for this snapshot</param>
/// <param name="Description">Description of the snapshot</param>
public sealed record CreateStateSnapshotCommand(
    Guid SessionStateId,
    int TurnNumber,
    string Description) : ICommand<GameStateSnapshotDto>;

using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to get all snapshots for a game session state.
/// Issue #2403: GameSessionState Entity (list snapshots for history)
/// </summary>
/// <param name="SessionStateId">ID of the game session state</param>
public sealed record GetStateSnapshotsQuery(Guid SessionStateId) : IQuery<List<GameStateSnapshotDto>>;

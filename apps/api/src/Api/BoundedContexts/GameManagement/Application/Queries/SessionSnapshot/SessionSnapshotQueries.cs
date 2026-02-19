using Api.BoundedContexts.GameManagement.Application.DTOs.SessionSnapshot;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.SessionSnapshot;

/// <summary>
/// Gets the list of all snapshots for a session (metadata only).
/// </summary>
internal sealed record GetSnapshotsQuery(Guid SessionId) : IQuery<IReadOnlyList<SessionSnapshotDto>>;

/// <summary>
/// Gets the reconstructed state at a specific snapshot index.
/// Applies checkpoint + deltas to reconstruct the full state.
/// </summary>
internal sealed record GetSnapshotStateQuery(Guid SessionId, int SnapshotIndex) : IQuery<ReconstructedStateDto?>;

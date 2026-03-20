using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.DTOs.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.Queries.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.SessionSnapshot;

/// <summary>
/// Returns the list of all snapshots for a session (metadata only).
/// </summary>
internal class GetSnapshotsQueryHandler
    : IQueryHandler<GetSnapshotsQuery, IReadOnlyList<SessionSnapshotDto>>
{
    private readonly ISessionSnapshotRepository _snapshotRepository;

    public GetSnapshotsQueryHandler(ISessionSnapshotRepository snapshotRepository)
    {
        _snapshotRepository = snapshotRepository ?? throw new ArgumentNullException(nameof(snapshotRepository));
    }

    public async Task<IReadOnlyList<SessionSnapshotDto>> Handle(
        GetSnapshotsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var snapshots = await _snapshotRepository.GetBySessionIdAsync(
            query.SessionId, cancellationToken).ConfigureAwait(false);

        return snapshots.Select(SessionSnapshotMapper.ToDto).ToList();
    }
}

/// <summary>
/// Reconstructs the full session state at a specific snapshot index.
/// Finds nearest checkpoint and applies deltas to rebuild the state.
/// Issue #4755: SessionSnapshot - Delta-based History + State Reconstruction.
/// </summary>
internal class GetSnapshotStateQueryHandler
    : IQueryHandler<GetSnapshotStateQuery, ReconstructedStateDto?>
{
    private readonly ISessionSnapshotRepository _snapshotRepository;

    public GetSnapshotStateQueryHandler(ISessionSnapshotRepository snapshotRepository)
    {
        _snapshotRepository = snapshotRepository ?? throw new ArgumentNullException(nameof(snapshotRepository));
    }

    public async Task<ReconstructedStateDto?> Handle(
        GetSnapshotStateQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Get all snapshots needed for reconstruction
        var snapshots = await _snapshotRepository.GetSnapshotsForReconstructionAsync(
            query.SessionId, query.SnapshotIndex, cancellationToken).ConfigureAwait(false);

        if (snapshots.Count == 0)
            return null;

        // The target snapshot (last in the list)
        var targetSnapshot = snapshots.LastOrDefault(s => s.SnapshotIndex == query.SnapshotIndex);
        if (targetSnapshot == null)
            return null;

        // First snapshot in list should be the checkpoint
        var checkpoint = snapshots[0];
        if (!checkpoint.IsCheckpoint)
            return null; // Corrupt data - no checkpoint found

        // Reconstruct: start with checkpoint, apply only deltas up to target index
        var checkpointState = checkpoint.DeltaDataJson;
        var deltas = snapshots
            .Where(s => s.SnapshotIndex > checkpoint.SnapshotIndex
                     && s.SnapshotIndex <= targetSnapshot.SnapshotIndex)
            .Select(s => s.DeltaDataJson);
        var reconstructedJson = JsonDeltaHelper.ReconstructState(checkpointState, deltas);

        try
        {
            using var doc = JsonDocument.Parse(reconstructedJson);
            return new ReconstructedStateDto(
                SnapshotIndex: targetSnapshot.SnapshotIndex,
                TurnIndex: targetSnapshot.TurnIndex,
                PhaseIndex: targetSnapshot.PhaseIndex,
                Timestamp: targetSnapshot.Timestamp,
                State: doc.RootElement.Clone());
        }
        catch (JsonException)
        {
            // Corrupted state data - treat as unavailable
            return null;
        }
    }
}

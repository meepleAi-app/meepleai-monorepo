using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.DTOs.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.SessionSnapshot;

/// <summary>
/// Creates a session snapshot with delta-based storage.
/// Snapshot 0 is always a full checkpoint; every 10th snapshot is a checkpoint.
/// Non-checkpoint snapshots store JSON Patch deltas only.
/// Issue #4755: SessionSnapshot - Delta-based History + State Reconstruction.
/// </summary>
internal class CreateSnapshotCommandHandler
    : ICommandHandler<CreateSnapshotCommand, SessionSnapshotDto>
{
    private readonly ISessionSnapshotRepository _snapshotRepository;
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSnapshotCommandHandler(
        ISessionSnapshotRepository snapshotRepository,
        ILiveSessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _snapshotRepository = snapshotRepository ?? throw new ArgumentNullException(nameof(snapshotRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<SessionSnapshotDto> Handle(
        CreateSnapshotCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Get the live session to capture current state
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        // Serialize current game state
        var currentStateJson = SerializeGameState(session.GameState);

        // Get latest snapshot to determine next index
        var latestSnapshot = await _snapshotRepository.GetLatestBySessionIdAsync(
            command.SessionId, cancellationToken).ConfigureAwait(false);

        var nextIndex = latestSnapshot == null ? 0 : latestSnapshot.SnapshotIndex + 1;
        var isCheckpoint = Domain.Entities.SessionSnapshot.SessionSnapshot.ShouldBeCheckpoint(nextIndex);

        string deltaDataJson;
        if (isCheckpoint || latestSnapshot == null)
        {
            // Store full state for checkpoints
            deltaDataJson = currentStateJson;
        }
        else
        {
            // Compute delta from previous state
            var previousState = await ReconstructStateAsync(
                command.SessionId, latestSnapshot.SnapshotIndex, cancellationToken)
                .ConfigureAwait(false);

            deltaDataJson = JsonDeltaHelper.ComputeDelta(previousState, currentStateJson);
        }

        var snapshot = new Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(),
            command.SessionId,
            nextIndex,
            command.TriggerType,
            command.TriggerDescription,
            deltaDataJson,
            isCheckpoint,
            session.CurrentTurnIndex,
            null, // PhaseIndex - will be used in Phase 3
            command.CreatedByPlayerId);

        await _snapshotRepository.AddAsync(snapshot, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return SessionSnapshotMapper.ToDto(snapshot);
    }

    private async Task<string> ReconstructStateAsync(
        Guid sessionId, int targetIndex, CancellationToken cancellationToken)
    {
        var snapshots = await _snapshotRepository.GetSnapshotsForReconstructionAsync(
            sessionId, targetIndex, cancellationToken).ConfigureAwait(false);

        if (snapshots.Count == 0)
            return "{}";

        // First snapshot should be the checkpoint (full state)
        var checkpoint = snapshots[0];
        if (!checkpoint.IsCheckpoint)
            throw new InvalidOperationException("State reconstruction failed: no checkpoint found");

        var state = checkpoint.DeltaDataJson;

        // Apply subsequent deltas
        for (int i = 1; i < snapshots.Count; i++)
        {
            state = JsonDeltaHelper.ApplyDelta(state, snapshots[i].DeltaDataJson);
        }

        return state;
    }

    private static string SerializeGameState(JsonDocument? gameState)
    {
        if (gameState == null) return "{}";

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        gameState.WriteTo(writer);
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}

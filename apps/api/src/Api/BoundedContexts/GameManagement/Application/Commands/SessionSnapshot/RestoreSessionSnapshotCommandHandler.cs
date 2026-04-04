using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.DTOs.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;

/// <summary>
/// Restores session state from a specific snapshot.
/// 1. Loads the target snapshot and reconstructs its state.
/// 2. Auto-creates a "Pre-restore" snapshot of the current state.
/// 3. Overwrites session's GameState, CurrentTurnIndex, and CurrentPhaseIndex.
/// Issue #5581: Auto-snapshot on Pause + snapshot history.
/// </summary>
internal sealed class RestoreSessionSnapshotCommandHandler
    : ICommandHandler<RestoreSessionSnapshotCommand, SessionSnapshotDto>
{
    private readonly ISessionSnapshotRepository _snapshotRepository;
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly IMediator _mediator;
    private readonly TimeProvider _timeProvider;

    public RestoreSessionSnapshotCommandHandler(
        ISessionSnapshotRepository snapshotRepository,
        ILiveSessionRepository sessionRepository,
        IMediator mediator,
        TimeProvider timeProvider)
    {
        _snapshotRepository = snapshotRepository ?? throw new ArgumentNullException(nameof(snapshotRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<SessionSnapshotDto> Handle(
        RestoreSessionSnapshotCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Validate session exists and is active
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        if (!session.IsActive)
            throw new ConflictException($"Cannot restore snapshot on a session in {session.Status} status. Must be active.");

        // 2. Validate target snapshot exists
        _ = await _snapshotRepository.GetBySessionAndIndexAsync(
            command.SessionId, command.SnapshotIndex, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("SessionSnapshot", $"Session {command.SessionId}, Index {command.SnapshotIndex}");

        // 3. Reconstruct the target state
        var snapshots = await _snapshotRepository.GetSnapshotsForReconstructionAsync(
            command.SessionId, command.SnapshotIndex, cancellationToken).ConfigureAwait(false);

        if (snapshots.Count == 0)
            throw new ConflictException("Cannot reconstruct snapshot state: no snapshots found for reconstruction.");

        var checkpoint = snapshots[0];
        if (!checkpoint.IsCheckpoint)
            throw new ConflictException("Cannot reconstruct snapshot state: no checkpoint found.");

        var reconstructedJson = checkpoint.DeltaDataJson;
        for (int i = 1; i < snapshots.Count; i++)
        {
            reconstructedJson = JsonDeltaHelper.ApplyDelta(reconstructedJson, snapshots[i].DeltaDataJson);
        }

        // 4. Auto-create "Pre-restore" snapshot of current state
        var preRestoreLabel = $"Auto \u2014 Pre-restore turno {session.CurrentTurnIndex}";
        var preRestoreSnapshot = await _mediator.Send(new CreateSnapshotCommand(
            command.SessionId,
            SnapshotTrigger.PreRestore,
            preRestoreLabel,
            CreatedByPlayerId: null), cancellationToken).ConfigureAwait(false);

        // 5. Restore: overwrite session state
        var restoredGameState = JsonDocument.Parse(reconstructedJson);
        session.UpdateGameState(restoredGameState, _timeProvider);

        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        return preRestoreSnapshot;
    }
}

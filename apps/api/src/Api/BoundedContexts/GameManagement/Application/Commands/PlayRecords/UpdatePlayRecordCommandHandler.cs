using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Handles updating play record details.
/// Issue #3889: CQRS commands for play records.
/// Issue #2437-3: snapshot pre-edit state as a version before mutating, cap to 5 most-recent.
/// Issue #2461: retry on version-number unique conflict (transparent server-side retry, no 500).
/// </summary>
internal class UpdatePlayRecordCommandHandler : ICommandHandler<UpdatePlayRecordCommand, UpdatePlayRecordResult>
{
    private readonly IPlayRecordRepository _recordRepository;
    private readonly IPlayRecordVersionRepository _versionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly PlayRecordPermissionChecker _permissionChecker;
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly Func<DbUpdateException, bool> _isVersionConflict;

    public UpdatePlayRecordCommandHandler(
        IPlayRecordRepository recordRepository,
        IPlayRecordVersionRepository versionRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        PlayRecordPermissionChecker permissionChecker,
        ILiveSessionRepository liveSessionRepository)
        : this(recordRepository, versionRepository, unitOfWork, timeProvider, permissionChecker,
               liveSessionRepository, PlayRecordVersionRepository.IsVersionNumberConflict)
    {
    }

    /// <summary>
    /// Internal constructor for testing — allows injecting a custom conflict detector so that
    /// tests can simulate a version-number unique violation without constructing a real
    /// <see cref="Npgsql.PostgresException"/> (whose properties are not publicly settable).
    /// </summary>
    internal UpdatePlayRecordCommandHandler(
        IPlayRecordRepository recordRepository,
        IPlayRecordVersionRepository versionRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        PlayRecordPermissionChecker permissionChecker,
        ILiveSessionRepository liveSessionRepository,
        Func<DbUpdateException, bool> isVersionConflict)
    {
        _recordRepository = recordRepository ?? throw new ArgumentNullException(nameof(recordRepository));
        _versionRepository = versionRepository ?? throw new ArgumentNullException(nameof(versionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
        _liveSessionRepository = liveSessionRepository ?? throw new ArgumentNullException(nameof(liveSessionRepository));
        _isVersionConflict = isVersionConflict ?? throw new ArgumentNullException(nameof(isVersionConflict));
    }

    /// <summary>
    /// Maximum number of times we retry the version+update save when a concurrent request
    /// races to assign the same version number. Each retry re-reads MAX and increments.
    /// In practice one retry always suffices; 5 guards against pathological concurrency.
    /// </summary>
    private const int MaxVersionConflictRetries = 5;

    public async Task<UpdatePlayRecordResult> Handle(UpdatePlayRecordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var record = await _recordRepository.GetByIdAsync(command.RecordId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("PlayRecord", command.RecordId.ToString());

        if (!await _permissionChecker.CanEditAsync(command.UserId, command.RecordId, cancellationToken).ConfigureAwait(false))
        {
            throw new ForbiddenException("You do not have permission to edit this play record.");
        }

        // #2437-3: snapshot the pre-edit state so this update is restorable.
        // Captured BEFORE mutating, so each version is a prior state
        // (the first update captures the initial state, making it undo-able).
        // Returns the staged version so we can identify it for the #2461 retry.
        var stagedVersion = await PlayRecordVersionSnapshotter.SnapshotCurrentAsync(
            _versionRepository, record, command.UserId, _timeProvider, cancellationToken)
            .ConfigureAwait(false);

        record.UpdateDetails(
            command.SessionDate,
            command.Notes,
            command.Location,
            _timeProvider);

        // #2437-1: stale-form optimistic concurrency. When the client sends the xmin it read,
        // push it so EF's concurrency check compares against the value the client saw — a
        // concurrent edit then yields DbUpdateConcurrencyException → 409. When absent, skip →
        // fresh-load behaviour (no check). Mirrors SharedGameTranslation.SetXmin(cmd.Xmin).
        if (command.Xmin.HasValue)
        {
            record.SetXmin(command.Xmin.Value);
        }

        await _recordRepository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);

        // #2461: retry on version-number unique conflict.
        // Two tabs / double-submit can read the same MAX → same VersionNumber → 23505.
        // On conflict: re-read MAX, update the tracked version entity's VersionNumber, retry.
        // This keeps the version + update save atomic. Cap at MaxVersionConflictRetries.
        var saveAttempt = 0;
        while (true)
        {
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                break; // success
            }
            catch (DbUpdateException ex) when (
                _isVersionConflict(ex)
                && saveAttempt < MaxVersionConflictRetries)
            {
                // Re-read MAX and update the still-tracked (Added) version entity so the
                // next SaveChangesAsync will use the correct, non-conflicting number.
                saveAttempt++;
                await _versionRepository.ReassignVersionNumberAsync(
                    command.RecordId, stagedVersion.Id, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        // Keep only the 5 most-recent versions per record (pruned AFTER the update save
        // so the new version is never accidentally deleted before it is persisted).
        await _versionRepository.PruneOldestAsync(
            command.RecordId, PlayRecordVersionSnapshotter.MaxVersions, cancellationToken)
            .ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // #13 / Invariante 4: the save is non-blocking, but if the same user has ANOTHER
        // session genuinely live (InProgress) right now, surface a non-blocking warning so
        // they can double-check the games were recorded in the right order. The repository
        // projects only the id (Setup/Paused do not count as "live").
        var liveSessionId = await _liveSessionRepository
            .GetActiveInProgressSessionIdAsync(command.UserId, cancellationToken)
            .ConfigureAwait(false);

        return liveSessionId is null
            ? new UpdatePlayRecordResult(false, null)
            : new UpdatePlayRecordResult(true, liveSessionId.Value);
    }
}

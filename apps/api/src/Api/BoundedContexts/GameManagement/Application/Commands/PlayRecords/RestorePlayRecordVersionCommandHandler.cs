using System.Globalization;
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
/// Handles restoring a play record to a previous version snapshot (creator-only).
/// Flow:
///   1. Load record — 404 if missing.
///   2. Permission check — 403 if not creator.
///   3. Load target version — 404 if missing.
///   4. Snapshot the CURRENT state (so the restore is undo-able).
///   5. Apply the target version's values via UpdateDetails.
///   6. Persist, then prune to the 5-version cap.
/// Issue #2437-3: version history + restore.
/// Issue #2461: retry on version-number unique conflict (transparent server-side retry).
/// </summary>
internal sealed class RestorePlayRecordVersionCommandHandler : ICommandHandler<RestorePlayRecordVersionCommand>
{
    private readonly IPlayRecordRepository _recordRepository;
    private readonly IPlayRecordVersionRepository _versionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly PlayRecordPermissionChecker _permissionChecker;
    private readonly Func<DbUpdateException, bool> _isVersionConflict;

    public RestorePlayRecordVersionCommandHandler(
        IPlayRecordRepository recordRepository,
        IPlayRecordVersionRepository versionRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        PlayRecordPermissionChecker permissionChecker)
        : this(recordRepository, versionRepository, unitOfWork, timeProvider, permissionChecker,
               PlayRecordVersionRepository.IsVersionNumberConflict)
    {
    }

    /// <summary>
    /// Internal constructor for testing — allows injecting a custom conflict detector so that
    /// tests can simulate a version-number unique violation without constructing a real
    /// <see cref="Npgsql.PostgresException"/> (whose properties are not publicly settable).
    /// </summary>
    internal RestorePlayRecordVersionCommandHandler(
        IPlayRecordRepository recordRepository,
        IPlayRecordVersionRepository versionRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        PlayRecordPermissionChecker permissionChecker,
        Func<DbUpdateException, bool> isVersionConflict)
    {
        _recordRepository = recordRepository ?? throw new ArgumentNullException(nameof(recordRepository));
        _versionRepository = versionRepository ?? throw new ArgumentNullException(nameof(versionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
        _isVersionConflict = isVersionConflict ?? throw new ArgumentNullException(nameof(isVersionConflict));
    }

    /// <summary>
    /// Maximum number of times we retry the version+update save when a concurrent request
    /// races to assign the same version number. Mirrors UpdatePlayRecordCommandHandler.
    /// </summary>
    private const int MaxVersionConflictRetries = 5;

    public async Task Handle(RestorePlayRecordVersionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Load record — 404 if not found
        var record = await _recordRepository.GetByIdAsync(command.RecordId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("PlayRecord", command.RecordId.ToString());

        // 2. Permission check — creator-only
        if (!await _permissionChecker.CanEditAsync(command.UserId, command.RecordId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new ForbiddenException("You do not have permission to restore this play record.");
        }

        // 3. Load the target version — 404 if not found
        var targetVersion = await _versionRepository
            .GetByVersionNumberAsync(command.RecordId, command.VersionNumber, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("PlayRecordVersion", command.VersionNumber.ToString(CultureInfo.InvariantCulture));

        // 4. Snapshot the CURRENT state BEFORE mutating so the restore is undo-able.
        // Returns the staged version so we can identify it for the #2461 retry.
        var stagedVersion = await PlayRecordVersionSnapshotter.SnapshotCurrentAsync(
            _versionRepository, record, command.UserId, _timeProvider, cancellationToken)
            .ConfigureAwait(false);

        // 5. Apply the target version's values
        record.UpdateDetails(targetVersion.SessionDate, targetVersion.Notes, targetVersion.Location, _timeProvider);

        // 6. Persist update + snapshot together, with #2461 retry on version-number conflict.
        await _recordRepository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);

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
                saveAttempt++;
                await _versionRepository.ReassignVersionNumberAsync(
                    command.RecordId, stagedVersion.Id, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        // Prune to the 5-version cap (post-save so the new snapshot is never deleted)
        await _versionRepository
            .PruneOldestAsync(command.RecordId, PlayRecordVersionSnapshotter.MaxVersions, cancellationToken)
            .ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Handles updating play record details.
/// Issue #3889: CQRS commands for play records.
/// Issue #2437-3: snapshot pre-edit state as a version before mutating, cap to 5 most-recent.
/// </summary>
internal class UpdatePlayRecordCommandHandler : ICommandHandler<UpdatePlayRecordCommand>
{
    private readonly IPlayRecordRepository _recordRepository;
    private readonly IPlayRecordVersionRepository _versionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly PlayRecordPermissionChecker _permissionChecker;

    public UpdatePlayRecordCommandHandler(
        IPlayRecordRepository recordRepository,
        IPlayRecordVersionRepository versionRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        PlayRecordPermissionChecker permissionChecker)
    {
        _recordRepository = recordRepository ?? throw new ArgumentNullException(nameof(recordRepository));
        _versionRepository = versionRepository ?? throw new ArgumentNullException(nameof(versionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
    }

    public async Task Handle(UpdatePlayRecordCommand command, CancellationToken cancellationToken)
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
        await PlayRecordVersionSnapshotter.SnapshotCurrentAsync(
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
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Keep only the 5 most-recent versions per record (pruned AFTER the update save
        // so the new version is never accidentally deleted before it is persisted).
        await _versionRepository.PruneOldestAsync(
            command.RecordId, PlayRecordVersionSnapshotter.MaxVersions, cancellationToken)
            .ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

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
/// </summary>
internal class UpdatePlayRecordCommandHandler : ICommandHandler<UpdatePlayRecordCommand>
{
    private readonly IPlayRecordRepository _recordRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly PlayRecordPermissionChecker _permissionChecker;

    public UpdatePlayRecordCommandHandler(
        IPlayRecordRepository recordRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        PlayRecordPermissionChecker permissionChecker)
    {
        _recordRepository = recordRepository ?? throw new ArgumentNullException(nameof(recordRepository));
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
    }
}

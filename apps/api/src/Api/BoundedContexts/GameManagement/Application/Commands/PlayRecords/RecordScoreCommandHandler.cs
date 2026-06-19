using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Handles recording a score for a player.
/// Issue #3889: CQRS commands for play records.
/// Issue #2349: Ownership check — only the record creator may record scores.
/// </summary>
internal class RecordScoreCommandHandler : ICommandHandler<RecordScoreCommand>
{
    private readonly IPlayRecordRepository _recordRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly PlayRecordPermissionChecker _permissionChecker;

    public RecordScoreCommandHandler(
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

    public async Task Handle(RecordScoreCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var record = await _recordRepository.GetByIdAsync(command.RecordId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("PlayRecord", command.RecordId.ToString());

        if (!await _permissionChecker.CanEditAsync(command.CallerUserId, command.RecordId, cancellationToken).ConfigureAwait(false))
        {
            throw new ForbiddenException("You do not have permission to record scores on this play record.");
        }

        var score = new RecordScore(command.Dimension, command.Value, command.Unit);
        record.RecordScore(command.PlayerId, score, _timeProvider);

        await _recordRepository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

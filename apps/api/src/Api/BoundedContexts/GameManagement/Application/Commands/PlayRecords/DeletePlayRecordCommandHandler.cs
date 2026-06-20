using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Handles soft-deleting a play record. Creator-only (issue #2439).
/// </summary>
internal class DeletePlayRecordCommandHandler : ICommandHandler<DeletePlayRecordCommand>
{
    private readonly IPlayRecordRepository _recordRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PlayRecordPermissionChecker _permissionChecker;

    public DeletePlayRecordCommandHandler(
        IPlayRecordRepository recordRepository,
        IUnitOfWork unitOfWork,
        PlayRecordPermissionChecker permissionChecker)
    {
        _recordRepository = recordRepository ?? throw new ArgumentNullException(nameof(recordRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
    }

    public async Task Handle(DeletePlayRecordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var record = await _recordRepository.GetByIdAsync(command.RecordId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("PlayRecord", command.RecordId.ToString());

        if (!await _permissionChecker.CanEditAsync(command.UserId, command.RecordId, cancellationToken).ConfigureAwait(false))
        {
            throw new ForbiddenException("You do not have permission to delete this play record.");
        }

        record.SoftDelete();

        await _recordRepository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

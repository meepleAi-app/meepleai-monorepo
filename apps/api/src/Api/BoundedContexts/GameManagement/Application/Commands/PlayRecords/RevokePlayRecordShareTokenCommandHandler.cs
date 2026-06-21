using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Handles revoking the public share token of a play record. Creator-only (#2437-2).
/// </summary>
internal class RevokePlayRecordShareTokenCommandHandler : ICommandHandler<RevokePlayRecordShareTokenCommand>
{
    private readonly IPlayRecordRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RevokePlayRecordShareTokenCommandHandler(
        IPlayRecordRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(RevokePlayRecordShareTokenCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var record = await _repository.GetByIdAsync(command.PlayRecordId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("PlayRecord", command.PlayRecordId.ToString());

        if (record.CreatedByUserId != command.UserId)
            throw new ForbiddenException("Only the record creator can revoke the share link.");

        record.RevokeShareToken();

        await _repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

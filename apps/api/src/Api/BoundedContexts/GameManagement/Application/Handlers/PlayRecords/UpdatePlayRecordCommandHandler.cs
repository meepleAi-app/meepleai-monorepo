using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.PlayRecords;

/// <summary>
/// Handles updating play record details.
/// Issue #3889: CQRS commands for play records.
/// </summary>
internal class UpdatePlayRecordCommandHandler : ICommandHandler<UpdatePlayRecordCommand>
{
    private readonly IPlayRecordRepository _recordRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public UpdatePlayRecordCommandHandler(
        IPlayRecordRepository recordRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _recordRepository = recordRepository ?? throw new ArgumentNullException(nameof(recordRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Handle(UpdatePlayRecordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var record = await _recordRepository.GetByIdAsync(command.RecordId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("PlayRecord", command.RecordId.ToString());

        record.UpdateDetails(
            command.SessionDate,
            command.Notes,
            command.Location,
            _timeProvider);

        await _recordRepository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

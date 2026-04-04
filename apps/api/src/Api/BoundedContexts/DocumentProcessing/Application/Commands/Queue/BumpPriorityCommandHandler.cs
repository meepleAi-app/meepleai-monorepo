using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Handles setting the priority of a queued processing job to an absolute value.
/// Issue #5455: Admin priority management.
/// </summary>
internal class SetPriorityCommandHandler : ICommandHandler<SetPriorityCommand>
{
    private readonly IProcessingJobRepository _jobRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SetPriorityCommandHandler(
        IProcessingJobRepository jobRepository,
        IUnitOfWork unitOfWork)
    {
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(SetPriorityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var job = await _jobRepository.GetByIdAsync(command.JobId, cancellationToken)
            .ConfigureAwait(false);

        if (job == null)
            throw new NotFoundException("ProcessingJob", command.JobId.ToString());

        job.UpdatePriority((int)command.NewPriority);

        await _jobRepository.UpdateAsync(job, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

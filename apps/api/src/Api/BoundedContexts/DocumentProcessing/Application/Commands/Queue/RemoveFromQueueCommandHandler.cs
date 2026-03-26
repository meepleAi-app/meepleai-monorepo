using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Handles removing a queued job from the queue.
/// Only Queued jobs can be removed.
/// Issue #4731: Queue commands.
/// </summary>
internal class RemoveFromQueueCommandHandler : ICommandHandler<RemoveFromQueueCommand>
{
    private readonly IProcessingJobRepository _jobRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveFromQueueCommandHandler(
        IProcessingJobRepository jobRepository,
        IUnitOfWork unitOfWork)
    {
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(RemoveFromQueueCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var job = await _jobRepository.GetByIdAsync(command.JobId, cancellationToken)
            .ConfigureAwait(false);
        if (job is null)
            throw new NotFoundException("ProcessingJob", command.JobId.ToString());

        if (job.Status != JobStatus.Queued)
            throw new ConflictException($"Cannot remove job with status '{job.Status}'. Only Queued jobs can be removed.");

        await _jobRepository.DeleteAsync(job, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

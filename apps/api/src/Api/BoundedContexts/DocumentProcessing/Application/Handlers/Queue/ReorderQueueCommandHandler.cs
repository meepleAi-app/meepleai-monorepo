using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

/// <summary>
/// Handles reordering queued jobs by updating their priority values.
/// Issue #4731: Queue commands.
/// </summary>
internal class ReorderQueueCommandHandler : ICommandHandler<ReorderQueueCommand>
{
    private readonly IProcessingJobRepository _jobRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReorderQueueCommandHandler(
        IProcessingJobRepository jobRepository,
        IUnitOfWork unitOfWork)
    {
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(ReorderQueueCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Load all referenced jobs and validate they are Queued
        for (var i = 0; i < command.OrderedJobIds.Count; i++)
        {
            var jobId = command.OrderedJobIds[i];
            var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
                .ConfigureAwait(false);
            if (job is null)
                throw new NotFoundException("ProcessingJob", jobId.ToString());

            if (job.Status != JobStatus.Queued)
                throw new ConflictException($"Cannot reorder job '{jobId}' with status '{job.Status}'. Only Queued jobs can be reordered.");

            // Priority = position in the list (lower = higher priority)
            job.UpdatePriority(i);

            await _jobRepository.UpdateAsync(job, cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

using Api.BoundedContexts.Administration.Application.Commands.BatchJobs;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.BatchJobs;

/// <summary>
/// Handler for canceling batch jobs (Issue #3693)
/// </summary>
internal sealed class CancelBatchJobCommandHandler : IRequestHandler<CancelBatchJobCommand>
{
    private readonly IBatchJobRepository _repository;

    public CancelBatchJobCommandHandler(IBatchJobRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task Handle(CancelBatchJobCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var job = await _repository.GetByIdAsync(request.JobId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("BatchJob", request.JobId.ToString());

        job.Cancel();

        await _repository.UpdateAsync(job, cancellationToken).ConfigureAwait(false);
    }
}

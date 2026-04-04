using Api.BoundedContexts.Administration.Application.Commands.BatchJobs;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.BatchJobs;

/// <summary>
/// Handler for creating batch jobs (Issue #3693)
/// </summary>
internal sealed class CreateBatchJobCommandHandler : IRequestHandler<CreateBatchJobCommand, Guid>
{
    private readonly IBatchJobRepository _repository;

    public CreateBatchJobCommandHandler(IBatchJobRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<Guid> Handle(CreateBatchJobCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var job = BatchJob.Create(request.Type, request.Parameters, request.CreatedBy);

        await _repository.AddAsync(job, cancellationToken).ConfigureAwait(false);

        return job.Id;
    }
}

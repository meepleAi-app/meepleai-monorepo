using Api.BoundedContexts.Administration.Application.Commands.BatchJobs;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers.BatchJobs;

/// <summary>
/// Handler for deleting batch jobs (Issue #3693)
/// </summary>
internal sealed class DeleteBatchJobCommandHandler : IRequestHandler<DeleteBatchJobCommand>
{
    private readonly IBatchJobRepository _repository;

    public DeleteBatchJobCommandHandler(IBatchJobRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task Handle(DeleteBatchJobCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _ = await _repository.GetByIdAsync(request.JobId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("BatchJob", request.JobId.ToString());

        await _repository.DeleteAsync(request.JobId, cancellationToken).ConfigureAwait(false);
    }
}

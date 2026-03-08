using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Handler
namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to retrieve a batch job by its ID.
/// Used by SSE streaming endpoint for initial job lookup.
/// </summary>
internal record GetBatchJobByIdQuery(Guid JobId) : IQuery<BatchJob?>;

internal class GetBatchJobByIdQueryHandler : IQueryHandler<GetBatchJobByIdQuery, BatchJob?>
{
    private readonly IBatchJobRepository _repository;

    public GetBatchJobByIdQueryHandler(IBatchJobRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<BatchJob?> Handle(GetBatchJobByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await _repository.GetByIdAsync(request.JobId, cancellationToken).ConfigureAwait(false);
    }
}

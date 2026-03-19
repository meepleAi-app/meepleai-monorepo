using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.BatchJobs;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.BatchJobs;

/// <summary>
/// Handler for getting a batch job by ID (Issue #3693)
/// </summary>
internal sealed class GetBatchJobQueryHandler : IRequestHandler<GetBatchJobQuery, BatchJobDto?>
{
    private readonly IBatchJobRepository _repository;

    public GetBatchJobQueryHandler(IBatchJobRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<BatchJobDto?> Handle(GetBatchJobQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var job = await _repository.GetByIdAsync(request.JobId, cancellationToken).ConfigureAwait(false);

        if (job == null)
            return null;

        return new BatchJobDto(
            job.Id,
            job.Type.ToString(),
            job.Status.ToString(),
            job.Progress,
            job.StartedAt,
            job.CompletedAt,
            job.DurationSeconds,
            job.ResultSummary,
            job.ErrorMessage,
            job.CreatedAt);
    }
}

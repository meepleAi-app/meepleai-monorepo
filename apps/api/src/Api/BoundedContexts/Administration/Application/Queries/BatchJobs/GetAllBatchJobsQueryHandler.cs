using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.BatchJobs;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.BatchJobs;

/// <summary>
/// Handler for getting all batch jobs with pagination (Issue #3693)
/// </summary>
internal sealed class GetAllBatchJobsQueryHandler : IRequestHandler<GetAllBatchJobsQuery, BatchJobListDto>
{
    private readonly IBatchJobRepository _repository;

    public GetAllBatchJobsQueryHandler(IBatchJobRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<BatchJobListDto> Handle(GetAllBatchJobsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var skip = (request.Page - 1) * request.PageSize;
        var jobs = await _repository.GetPagedAsync(skip, request.PageSize, request.Status, cancellationToken)
            .ConfigureAwait(false);

        var total = await _repository.CountByStatusAsync(request.Status, cancellationToken)
            .ConfigureAwait(false);

        var jobDtos = jobs.Select(job => new BatchJobDto(
            job.Id,
            job.Type.ToString(),
            job.Status.ToString(),
            job.Progress,
            job.StartedAt,
            job.CompletedAt,
            job.DurationSeconds,
            job.ResultSummary,
            job.ErrorMessage,
            job.CreatedAt)).ToList();

        return new BatchJobListDto(jobDtos, total);
    }
}

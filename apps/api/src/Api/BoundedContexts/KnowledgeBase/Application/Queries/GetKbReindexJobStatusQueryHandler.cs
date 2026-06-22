using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for <see cref="GetKbReindexJobStatusQuery"/>. Issue #941 / ADR-057.
/// Returns null when the job ID does not exist; throws <see cref="NotFoundException"/>
/// when the job exists but is for a different game (path mismatch); throws
/// <see cref="ForbiddenException"/> when the requesting user is not the job owner.
/// </summary>
internal sealed class GetKbReindexJobStatusQueryHandler
    : IQueryHandler<GetKbReindexJobStatusQuery, KbReindexJobStatusDto?>
{
    private readonly IKbReindexJobRepository _jobRepository;

    public GetKbReindexJobStatusQueryHandler(IKbReindexJobRepository jobRepository)
    {
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
    }

    public async Task<KbReindexJobStatusDto?> Handle(
        GetKbReindexJobStatusQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var job = await _jobRepository.GetByIdAsync(query.JobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
            return null;

        if (job.GameId != query.GameId)
            throw new NotFoundException($"Job {query.JobId} not found for game {query.GameId}");

        if (job.UserId != query.RequestingUserId)
            throw new ForbiddenException($"Job {query.JobId} belongs to another user");

        return new KbReindexJobStatusDto(
            JobId: job.Id,
            GameId: job.GameId,
            Status: job.Status,
            TotalPdfs: job.TotalPdfs,
            ProcessedPdfs: job.ProcessedPdfs,
            CreatedAt: job.CreatedAt,
            StartedAt: job.StartedAt,
            CompletedAt: job.CompletedAt,
            FailureReason: job.FailureReason);
    }
}

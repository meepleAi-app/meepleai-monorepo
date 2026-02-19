using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

/// <summary>
/// Handles fetching detailed job information including steps and log entries.
/// Reads directly from EF entities (CQRS read side).
/// Issue #4731: Queue queries.
/// </summary>
internal class GetJobDetailQueryHandler : IQueryHandler<GetJobDetailQuery, ProcessingJobDetailDto>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetJobDetailQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ProcessingJobDetailDto> Handle(GetJobDetailQuery query, CancellationToken cancellationToken)
    {
        var job = await _dbContext.ProcessingJobs
            .AsNoTracking()
            .Include(j => j.PdfDocument)
            .Include(j => j.Steps)
                .ThenInclude(s => s.LogEntries)
            .FirstOrDefaultAsync(j => j.Id == query.JobId, cancellationToken)
            .ConfigureAwait(false);

        if (job is null)
            throw new NotFoundException("ProcessingJob", query.JobId.ToString());

        var steps = job.Steps
            .OrderBy(s => s.StepName, StringComparer.Ordinal)
            .Select(s => new ProcessingStepDto(
                s.Id,
                s.StepName,
                s.Status,
                s.StartedAt,
                s.CompletedAt,
                s.DurationMs,
                s.MetadataJson,
                s.LogEntries
                    .OrderBy(l => l.Timestamp)
                    .Select(l => new StepLogEntryDto(
                        l.Id,
                        l.Timestamp,
                        l.Level,
                        l.Message
                    ))
                    .ToList()
            ))
            .ToList();

        var canRetry = string.Equals(job.Status, "Failed", StringComparison.Ordinal) && job.RetryCount < job.MaxRetries;

        return new ProcessingJobDetailDto(
            job.Id,
            job.PdfDocumentId,
            job.PdfDocument.FileName,
            job.UserId,
            job.Status,
            job.Priority,
            job.CurrentStep,
            job.CreatedAt,
            job.StartedAt,
            job.CompletedAt,
            job.ErrorMessage,
            job.RetryCount,
            job.MaxRetries,
            canRetry,
            steps
        );
    }
}

using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;

/// <summary>
/// Fetches the most recent ProcessingJob (with Steps and LogEntries) for a given PdfDocumentId.
/// Returns null when no job exists for the document (e.g. legacy PDFs predating the pipeline).
/// Issue #1650: KB Ingestion log tab.
/// </summary>
internal sealed class GetLatestIngestionLogByDocumentIdQueryHandler
    : IQueryHandler<GetLatestIngestionLogByDocumentIdQuery, ProcessingJobDetailDto?>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetLatestIngestionLogByDocumentIdQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ProcessingJobDetailDto?> Handle(
        GetLatestIngestionLogByDocumentIdQuery query, CancellationToken cancellationToken)
    {
        if (query.DocumentId == Guid.Empty) return null;

        var job = await _dbContext.ProcessingJobs
            .AsNoTracking()
            .Include(j => j.PdfDocument)
            .Include(j => j.Steps)
                .ThenInclude(s => s.LogEntries)
            .Where(j => j.PdfDocumentId == query.DocumentId)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (job is null) return null;

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
                        l.Message))
                    .ToList()))
            .ToList();

        var canRetry = string.Equals(job.Status, "Failed", StringComparison.Ordinal)
                       && job.RetryCount < job.MaxRetries;

        return new ProcessingJobDetailDto(
            job.Id,
            job.PdfDocumentId,
            job.PdfDocument?.FileName ?? string.Empty, // Defensive: PdfDocumentId is a non-nullable FK, but guard against ORM edge cases.
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
            steps);
    }
}

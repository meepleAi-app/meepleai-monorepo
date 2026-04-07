using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;

/// <summary>
/// Query to get ETA estimates for all queued and processing jobs.
/// Returns per-job ETAs and total estimated queue drain time.
/// </summary>
internal sealed record GetBatchETAQuery() : IQuery<BatchETAResult>;

internal sealed record BatchETAResult(
    IReadOnlyList<JobETADto> Jobs,
    double TotalDrainTimeMinutes,
    int JobCount);

internal sealed record JobETADto(
    Guid JobId,
    Guid PdfDocumentId,
    string Status,
    string? CurrentStep,
    double? EstimatedMinutesRemaining);

internal sealed class GetBatchETAQueryHandler : IQueryHandler<GetBatchETAQuery, BatchETAResult>
{
    private readonly IProcessingJobRepository _jobRepository;
    private readonly IProcessingMetricsService _metricsService;

    public GetBatchETAQueryHandler(
        IProcessingJobRepository jobRepository,
        IProcessingMetricsService metricsService)
    {
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
    }

    public async Task<BatchETAResult> Handle(GetBatchETAQuery query, CancellationToken cancellationToken)
    {
        var queuedJobs = await _jobRepository.GetAllByStatusAsync(JobStatus.Queued, cancellationToken).ConfigureAwait(false);
        var processingJobs = await _jobRepository.GetAllByStatusAsync(JobStatus.Processing, cancellationToken).ConfigureAwait(false);

        var allJobs = processingJobs.Concat(queuedJobs).ToList();
        var jobEtas = new List<JobETADto>(allJobs.Count);
        var totalDrainTime = TimeSpan.Zero;

        foreach (var job in allJobs)
        {
            var processingState = MapToProcessingState(job.CurrentStep);
            TimeSpan? eta = null;

            if (processingState is not null)
            {
                eta = await _metricsService.CalculateETAAsync(
                    job.PdfDocumentId,
                    processingState.Value,
                    cancellationToken).ConfigureAwait(false);
            }

            if (eta.HasValue)
            {
                totalDrainTime += eta.Value;
            }

            jobEtas.Add(new JobETADto(
                JobId: job.Id,
                PdfDocumentId: job.PdfDocumentId,
                Status: job.Status.ToString(),
                CurrentStep: job.CurrentStep?.ToString(),
                EstimatedMinutesRemaining: eta.HasValue
                    ? Math.Round(eta.Value.TotalMinutes, 1)
                    : null));
        }

        return new BatchETAResult(
            Jobs: jobEtas,
            TotalDrainTimeMinutes: Math.Round(totalDrainTime.TotalMinutes, 1),
            JobCount: allJobs.Count);
    }

    /// <summary>
    /// Maps ProcessingStepType (job pipeline step) to PdfProcessingState (metrics state).
    /// Returns null for queued jobs with no current step, meaning no ETA data available.
    /// </summary>
    private static PdfProcessingState? MapToProcessingState(ProcessingStepType? stepType)
    {
        return stepType switch
        {
            ProcessingStepType.Upload => PdfProcessingState.Uploading,
            ProcessingStepType.Extract => PdfProcessingState.Extracting,
            ProcessingStepType.Chunk => PdfProcessingState.Chunking,
            ProcessingStepType.Embed => PdfProcessingState.Embedding,
            ProcessingStepType.Index => PdfProcessingState.Indexing,
            null => PdfProcessingState.Pending,
            _ => null
        };
    }
}

using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Quartz;

namespace Api.BoundedContexts.Administration.Infrastructure.Scheduling;

/// <summary>
/// Background job processor for batch job queue (Issue #3693 - Task 2)
/// Processes queued jobs every 30 seconds
/// </summary>
[DisallowConcurrentExecution]
internal sealed class BatchJobProcessorJob : IJob
{
    private readonly IBatchJobRepository _repository;
    private readonly ILogger<BatchJobProcessorJob> _logger;

    public BatchJobProcessorJob(
        IBatchJobRepository repository,
        ILogger<BatchJobProcessorJob> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var queuedJobs = await _repository.GetByStatusAsync(JobStatus.Queued, context.CancellationToken).ConfigureAwait(false);

            if (queuedJobs.Count == 0) return;

            // Process first job (FIFO)
            var job = queuedJobs.OrderBy(j => j.CreatedAt).First();

            _logger.LogInformation("Processing batch job {JobId} of type {JobType}", job.Id, job.Type);

            job.Start();
            await _repository.UpdateAsync(job, context.CancellationToken).ConfigureAwait(false);

            try
            {
                await ExecuteJobAsync(job, context.CancellationToken).ConfigureAwait(false);

                job.Complete(
                    $"{{\"success\":true,\"completedAt\":\"{DateTime.UtcNow:O}\"}}",
                    $"{job.Type} completed successfully",
                    null);

                _logger.LogInformation("Batch job {JobId} completed", job.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch job {JobId} failed", job.Id);
                job.Fail(ex.Message, ex.StackTrace);
            }

            await _repository.UpdateAsync(job, context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch job processor");
        }
    }

    private async Task ExecuteJobAsync(BatchJob job, CancellationToken ct)
    {
        // Simulate progress updates
        for (var p = 0; p <= 100; p += 20)
        {
            job.UpdateProgress(p);
            await _repository.UpdateAsync(job, ct).ConfigureAwait(false);
            await Task.Delay(1000, ct).ConfigureAwait(false);
        }

        // Job-specific logic (placeholder)
        await (job.Type switch
        {
            JobType.ResourceForecast => Task.CompletedTask,
            JobType.CostAnalysis => Task.CompletedTask,
            JobType.DataCleanup => Task.CompletedTask,
            JobType.BggSync => Task.CompletedTask,
            JobType.AgentBenchmark => Task.CompletedTask,
            _ => throw new NotSupportedException($"Job type {job.Type} not supported")
        }).ConfigureAwait(false);
    }
}

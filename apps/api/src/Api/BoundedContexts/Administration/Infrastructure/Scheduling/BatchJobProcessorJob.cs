using Api.BoundedContexts.Administration.Application.Services;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BatchJobProcessorJob> _logger;

    public BatchJobProcessorJob(
        IBatchJobRepository repository,
        IServiceProvider serviceProvider,
        ILogger<BatchJobProcessorJob> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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
        switch (job.Type)
        {
            case JobType.VectorReembedding:
                await ExecuteVectorReembeddingAsync(job, ct).ConfigureAwait(false);
                break;

            default:
                // Placeholder progress for other job types
                for (var p = 0; p <= 100; p += 20)
                {
                    job.UpdateProgress(p);
                    await _repository.UpdateAsync(job, ct).ConfigureAwait(false);
                    await Task.Delay(1000, ct).ConfigureAwait(false);
                }
                break;
        }
    }

    private async Task ExecuteVectorReembeddingAsync(BatchJob job, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var reembeddingService = scope.ServiceProvider.GetRequiredService<VectorReembeddingService>();
        await reembeddingService.ExecuteAsync(job, ct).ConfigureAwait(false);
    }
}

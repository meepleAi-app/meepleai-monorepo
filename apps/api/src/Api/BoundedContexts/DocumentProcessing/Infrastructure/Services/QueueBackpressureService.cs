using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Calculates queue backpressure based on pending job count and average processing time.
/// Issue #5457: Backpressure when queue is full.
/// </summary>
internal sealed class QueueBackpressureService : IQueueBackpressureService
{
    public const int DefaultBackpressureThreshold = 50;
    public static readonly TimeSpan DefaultAvgProcessingTime = TimeSpan.FromMinutes(3);

    private readonly IProcessingJobRepository _jobRepository;
    private readonly IProcessingQueueConfigRepository _configRepository;

    public QueueBackpressureService(
        IProcessingJobRepository jobRepository,
        IProcessingQueueConfigRepository configRepository)
    {
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
    }

    public async Task<BackpressureResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var queuedCount = await _jobRepository.CountByStatusAsync(JobStatus.Queued, cancellationToken)
            .ConfigureAwait(false);
        var processingCount = await _jobRepository.CountProcessingAsync(cancellationToken)
            .ConfigureAwait(false);

        var config = await _configRepository.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
        var maxWorkers = config.MaxConcurrentWorkers;

        var totalPending = queuedCount + processingCount;
        var isUnderPressure = totalPending >= DefaultBackpressureThreshold;

        // ETA = (queued jobs / max workers) * avg processing time
        var estimatedWaitTime = maxWorkers > 0
            ? TimeSpan.FromTicks(DefaultAvgProcessingTime.Ticks * queuedCount / maxWorkers)
            : TimeSpan.Zero;

        return new BackpressureResult(
            QueueDepth: totalPending,
            BackpressureThreshold: DefaultBackpressureThreshold,
            IsUnderPressure: isUnderPressure,
            EstimatedWaitTime: estimatedWaitTime,
            MaxConcurrentWorkers: maxWorkers);
    }
}

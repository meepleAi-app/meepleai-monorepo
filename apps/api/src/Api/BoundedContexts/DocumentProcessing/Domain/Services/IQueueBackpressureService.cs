namespace Api.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// Checks queue depth and calculates estimated wait time for backpressure.
/// Issue #5457: Backpressure when queue is full.
/// </summary>
internal interface IQueueBackpressureService
{
    /// <summary>
    /// Check if the queue is under backpressure (> threshold pending items).
    /// Returns queue depth, estimated wait time, and whether uploads should be throttled.
    /// </summary>
    Task<BackpressureResult> CheckAsync(CancellationToken cancellationToken = default);
}

internal sealed record BackpressureResult(
    int QueueDepth,
    int BackpressureThreshold,
    bool IsUnderPressure,
    TimeSpan EstimatedWaitTime,
    int MaxConcurrentWorkers);

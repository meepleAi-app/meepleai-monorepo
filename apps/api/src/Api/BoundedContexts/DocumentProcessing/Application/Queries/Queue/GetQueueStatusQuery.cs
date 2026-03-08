using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;

/// <summary>
/// Query to get current queue status including backpressure info and ETA.
/// Issue #5457: Backpressure + ETA display.
/// </summary>
internal sealed record GetQueueStatusQuery() : IQuery<QueueStatusDto>;

internal sealed record QueueStatusDto(
    int QueueDepth,
    int BackpressureThreshold,
    bool IsUnderPressure,
    bool IsPaused,
    int MaxConcurrentWorkers,
    double EstimatedWaitMinutes);

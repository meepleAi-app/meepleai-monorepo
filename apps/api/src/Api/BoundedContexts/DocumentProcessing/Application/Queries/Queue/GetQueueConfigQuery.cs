using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;

/// <summary>
/// Gets the current processing queue configuration.
/// Issue #5455: Queue configuration management.
/// </summary>
internal record GetQueueConfigQuery() : IQuery<QueueConfigDto>;

internal record QueueConfigDto(
    bool IsPaused,
    int MaxConcurrentWorkers,
    DateTimeOffset UpdatedAt,
    Guid? UpdatedBy);

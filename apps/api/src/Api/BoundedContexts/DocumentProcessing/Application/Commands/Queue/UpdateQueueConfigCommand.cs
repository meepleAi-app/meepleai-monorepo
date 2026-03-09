using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Updates the processing queue configuration (pause/resume, concurrency).
/// Issue #5455: Queue configuration management.
/// </summary>
internal record UpdateQueueConfigCommand(
    Guid UpdatedBy,
    bool? IsPaused = null,
    int? MaxConcurrentWorkers = null
) : ICommand;
